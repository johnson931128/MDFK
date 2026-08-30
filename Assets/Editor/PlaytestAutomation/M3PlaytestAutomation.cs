#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace MDFK.Editor.Playtests
{
    public static class M3PlaytestAutomation
    {
        private const string ScenePath = "Assets/Scenes/M3_Greybox.unity";
        private const string BandicamPath = @"C:\Program Files\Bandicam\bdcam.exe";
        private const string BandicamOutputFolder = @"C:\Users\fanshunjie\Documents\Bandicam";
        private const string ArtifactPath = "Artifacts/Playtests/r01-background-pass-01.mp4";
        private const double TestDurationSeconds = 12.0;
        private const double InputUpdateIntervalSeconds = 0.05;
        private const double PlayWarmupSeconds = 0.5;
        private const double OutputWaitSeconds = 12.0;

        private static bool running;
        private static bool failed;
        private static double stateStartedAt;
        private static double recordingStartedAt;
        private static double outputWaitStartedAt;
        private static readonly HashSet<string> filesBeforeRecording = new(StringComparer.OrdinalIgnoreCase);
        private static string stableCandidateFile;
        private static long stableCandidateSize = -1;
        private static int stableCandidatePolls;
        private static GameObject player;
        private static Rigidbody2D playerBody;
        private static Vector3 initialPlayerPosition;
        private static float maximumHorizontalDisplacement;
        private static float maximumRise;
        private static float minimumRise;
        private static double lastInputUpdateAt;
        private static bool jumpObserved;
        private static bool movementObserved;

        private enum Phase
        {
            WaitingForPlay,
            PlayWarmup,
            RecordingInput,
            StoppingRecording,
            WaitingForOutput
        }

        private static Phase phase;

        [MenuItem("MDFK/Playtests/Run M3 Background Pass")]
        public static void Run()
        {
            if (running)
            {
                UnityEngine.Debug.LogWarning("M3 playtest automation is already running.");
                return;
            }

            if (!File.Exists(ToAbsolutePath(ScenePath)))
            {
                UnityEngine.Debug.LogError("Playtest scene was not found: " + ScenePath);
                return;
            }

            if (!File.Exists(BandicamPath))
            {
                UnityEngine.Debug.LogError("Bandicam executable was not found: " + BandicamPath);
                return;
            }

            if (!Directory.Exists(BandicamOutputFolder))
            {
                UnityEngine.Debug.LogError("Bandicam output folder was not found: " + BandicamOutputFolder);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogError("Playtest requires the Editor to be stopped before starting.");
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                UnityEngine.Debug.LogError("Playtest scene could not be loaded: " + ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                UnityEngine.Debug.LogError("Playtest scene could not be opened: " + ScenePath);
                return;
            }

            ResetRunState();
            running = true;
            phase = Phase.WaitingForPlay;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
            UnityEngine.Debug.Log("M3_PLAYTEST_START scene=" + ScenePath + " duration=" + TestDurationSeconds.ToString("F1") + "s");
        }

        private static void Tick()
        {
            if (!running)
            {
                return;
            }

            try
            {
                switch (phase)
                {
                    case Phase.WaitingForPlay:
                        WaitForPlay();
                        break;
                    case Phase.PlayWarmup:
                        WaitForPlayer();
                        break;
                    case Phase.RecordingInput:
                        RunInputSchedule();
                        break;
                    case Phase.StoppingRecording:
                        FinishRecording();
                        break;
                    case Phase.WaitingForOutput:
                        WaitForRecordedFile();
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail("M3 playtest automation failed: " + exception.Message);
                StopInput();
                StopBandicam();
                LeavePlayMode();
            }
        }

        private static void WaitForPlay()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            stateStartedAt = EditorApplication.timeSinceStartup;
            phase = Phase.PlayWarmup;
        }

        private static void WaitForPlayer()
        {
            if (!EditorApplication.isPlaying)
            {
                Fail("Editor left Play Mode before the automated test could start.");
                Cleanup();
                return;
            }

            player = GameObject.Find("Player");
            if (player == null)
            {
                Fail("Player was not found in Play Mode.");
                Cleanup();
                return;
            }

            playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody == null)
            {
                Fail("Player Rigidbody2D was not found.");
                Cleanup();
                return;
            }

            if (EditorApplication.timeSinceStartup - stateStartedAt < PlayWarmupSeconds)
            {
                return;
            }

            initialPlayerPosition = player.transform.position;
            maximumHorizontalDisplacement = 0f;
            maximumRise = 0f;
            minimumRise = 0f;
            recordingStartedAt = EditorApplication.timeSinceStartup;
            lastInputUpdateAt = double.NegativeInfinity;
            SnapshotBandicamFiles();
            StartBandicam();
            phase = Phase.RecordingInput;
            UnityEngine.Debug.Log("M3_PLAYTEST_RECORDING_STARTED input=KeyboardState QueueStateEvent");
        }

        private static void RunInputSchedule()
        {
            if (!EditorApplication.isPlaying)
            {
                Fail("Editor left Play Mode during the automated test.");
                StopInput();
                StopBandicam();
                Cleanup();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - recordingStartedAt;
            ObservePlayer();

            if (elapsed - lastInputUpdateAt >= InputUpdateIntervalSeconds)
            {
                lastInputUpdateAt = elapsed;
                QueueScheduledKeyboardState(elapsed);
            }

            if (elapsed >= TestDurationSeconds)
            {
                StopInput();
                StopBandicam();
                phase = Phase.StoppingRecording;
                stateStartedAt = EditorApplication.timeSinceStartup;
            }
        }

        private static void FinishRecording()
        {
            ObservePlayer();
            StopInput();

            if (EditorApplication.isPlaying)
            {
                LeavePlayMode();
            }

            outputWaitStartedAt = EditorApplication.timeSinceStartup;
            phase = Phase.WaitingForOutput;
        }

        private static void WaitForRecordedFile()
        {
            string latest = FindNewBandicamFile();
            if (!string.IsNullOrEmpty(latest))
            {
                string destination = ToAbsolutePath(ArtifactPath);
                string destinationDirectory = Path.GetDirectoryName(destination);
                Directory.CreateDirectory(destinationDirectory);
                File.Copy(latest, destination, true);
                FileInfo copied = new(destination);
                UnityEngine.Debug.Log("M3_PLAYTEST_RECORDING_STOPPED latest=" + latest);
                UnityEngine.Debug.Log("M3_PLAYTEST_FILE_COPIED path=" + destination + " bytes=" + copied.Length);
                UnityEngine.Debug.Log("M3_PLAYTEST_INPUT_RESULT moved=" + movementObserved + " maxHorizontal=" + maximumHorizontalDisplacement.ToString("F3") + " jumped=" + jumpObserved + " maxRise=" + maximumRise.ToString("F3"));
                Cleanup();
                return;
            }

            if (EditorApplication.timeSinceStartup - outputWaitStartedAt >= OutputWaitSeconds)
            {
                Fail("Bandicam stopped, but no new MP4 appeared in " + BandicamOutputFolder);
                Cleanup();
            }
        }

        private static void QueueScheduledKeyboardState(double elapsed)
        {
            if (Keyboard.current == null)
            {
                throw new InvalidOperationException("Unity Input System Keyboard.current is unavailable.");
            }

            bool moveRight = elapsed >= 1.0 && elapsed < 4.0;
            bool moveLeft = elapsed >= 5.0 && elapsed < 8.0;
            bool jumpRight = elapsed >= 2.0 && elapsed < 2.15;
            bool jumpLeft = elapsed >= 6.5 && elapsed < 6.65;

            if (moveRight)
            {
                QueueKeyboard(jumpRight ? new[] { Key.D, Key.Space } : new[] { Key.D });
            }
            else if (moveLeft)
            {
                QueueKeyboard(jumpLeft ? new[] { Key.A, Key.Space } : new[] { Key.A });
            }
            else
            {
                QueueKeyboard();
            }
        }

        private static void QueueKeyboard(params Key[] pressedKeys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(pressedKeys));
        }

        private static void ObservePlayer()
        {
            if (player == null || playerBody == null)
            {
                return;
            }

            Vector3 position = player.transform.position;
            float horizontalDisplacement = Mathf.Abs(position.x - initialPlayerPosition.x);
            float rise = position.y - initialPlayerPosition.y;
            maximumHorizontalDisplacement = Mathf.Max(maximumHorizontalDisplacement, horizontalDisplacement);
            maximumRise = Mathf.Max(maximumRise, rise);
            minimumRise = Mathf.Min(minimumRise, rise);
            movementObserved |= horizontalDisplacement >= 0.2f;
            jumpObserved |= rise >= 0.25f || playerBody.linearVelocity.y >= 1.0f;
        }

        private static void StartBandicam()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BandicamPath,
                Arguments = "/record",
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }

        private static void StopBandicam()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BandicamPath,
                Arguments = "/stop",
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }

        private static void SnapshotBandicamFiles()
        {
            filesBeforeRecording.Clear();
            foreach (string file in Directory.GetFiles(BandicamOutputFolder, "*.mp4"))
            {
                filesBeforeRecording.Add(Path.GetFullPath(file));
            }
        }

        private static string FindNewBandicamFile()
        {
            FileInfo candidate = Directory.GetFiles(BandicamOutputFolder, "*.mp4")
                .Select(Path.GetFullPath)
                .Where(file => !filesBeforeRecording.Contains(file))
                .Select(file => new FileInfo(file))
                .Where(file => file.LastWriteTimeUtc >= DateTime.UtcNow.AddSeconds(-OutputWaitSeconds - 2.0))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (candidate == null)
            {
                stableCandidateFile = null;
                stableCandidateSize = -1;
                stableCandidatePolls = 0;
                return null;
            }

            if (string.Equals(candidate.FullName, stableCandidateFile, StringComparison.OrdinalIgnoreCase)
                && candidate.Length == stableCandidateSize)
            {
                stableCandidatePolls++;
            }
            else
            {
                stableCandidateFile = candidate.FullName;
                stableCandidateSize = candidate.Length;
                stableCandidatePolls = 1;
            }

            if (stableCandidatePolls < 3 || !HasMp4MovieBox(candidate.FullName))
            {
                return null;
            }

            return candidate.FullName;
        }

        private static bool HasMp4MovieBox(string filePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                byte[] movieBox = { (byte)'m', (byte)'o', (byte)'o', (byte)'v' };
                for (int i = 0; i <= bytes.Length - movieBox.Length; i++)
                {
                    if (bytes[i] == movieBox[0]
                        && bytes[i + 1] == movieBox[1]
                        && bytes[i + 2] == movieBox[2]
                        && bytes[i + 3] == movieBox[3])
                    {
                        return true;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }

            return false;
        }

        private static void StopInput()
        {
            if (Keyboard.current != null)
            {
                QueueKeyboard();
            }
        }

        private static void LeavePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void Fail(string message)
        {
            failed = true;
            UnityEngine.Debug.LogError(message);
        }

        private static void Cleanup()
        {
            StopInput();
            EditorApplication.update -= Tick;
            running = false;
            phase = Phase.WaitingForPlay;
            if (!failed)
            {
                UnityEngine.Debug.Log("M3_PLAYTEST_COMPLETE");
            }
        }

        private static void ResetRunState()
        {
            failed = false;
            player = null;
            playerBody = null;
            maximumHorizontalDisplacement = 0f;
            maximumRise = 0f;
            minimumRise = 0f;
            jumpObserved = false;
            movementObserved = false;
            filesBeforeRecording.Clear();
            stableCandidateFile = null;
            stableCandidateSize = -1;
            stableCandidatePolls = 0;
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        }
    }
}
#endif
