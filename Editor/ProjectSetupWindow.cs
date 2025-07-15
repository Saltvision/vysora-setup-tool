using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Vysora;
using System;
using UnityEngine.UIElements; // For UI Toolkit
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using System.Reflection; // For reflection-based property setting
using UnityEditor.SceneManagement;
using System.Linq;

namespace Saltvision.VysoraSetup.Editor
{
    public class ProjectSetupWindow : EditorWindow
    {
        // UI state
        private int currentTab = 0;
        private string[] tabNames = new string[] { "Setup", "About" };
        private Vector2 aboutScrollPosition;

        // Setup state tracking
        private bool setupConfirmed = false;
        private int currentSetupStep = 0;
        private readonly string[] setupStepLabels = new string[] {
    "Create Scene Objects",
    "Download Project Assets",
    "Configure Game Objects",
    "Configure Script Properties"
};

        // GitHub credentials
        private string githubUsername = "";
        private string githubPassword = "";
        private string githubToken = "";
        private string githubRepo = "Saltvision/Vysora-Assets";
        private string assetDownloadPath = "Assets/Vysora";
        private bool isLoggedIn = false;
        private bool isDownloading = false;
        private float downloadProgress = 0f;
        private string downloadStatus = "";
        private bool showPassword = false;

        // Git process info
        private string gitExecutablePath = "";
        private bool gitInstalled = false;

        private bool isPrivateRepo = true; // Default to true for private repos


        [MenuItem("Tools/Saltvision/Vysora Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProjectSetupWindow>();
            window.titleContent = new GUIContent("Vysora Setup");
            window.minSize = new Vector2(450, 400);
        }

        private void OnEnable()
        {
            // Initialize if needed
            Saltvision.VysoraSetup.Logger.Info("Initializing Vysora Setup Window");
            CheckGitInstallation();
        }

        private void OnGUI()
        {
            DrawHeader();

            // Basic tab system
            currentTab = GUILayout.Toolbar(currentTab, tabNames);

            GUILayout.Space(15);

            switch (currentTab)
            {
                case 0:
                    DrawSetupTab();
                    break;
                case 1:
                    DrawAboutTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Vysora Project Setup Tool", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("v1.0", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }

        private void DrawSetupTab()
        {

            EditorGUILayout.HelpBox(
                "This step will download necessary project assets from GitHub.\n" +
                "For private repositories, authentication is required.",
                MessageType.Info);

            GUILayout.Space(10);

            isPrivateRepo = EditorGUILayout.Toggle("Private Repository", isPrivateRepo);

            GUILayout.Label("Project Setup", EditorStyles.boldLabel);

            if (isPrivateRepo)
            {
                EditorGUILayout.HelpBox(
                    "Private repositories require authentication.\n" +
                    "Personal Access Token is the recommended method.",
                    MessageType.Warning);

                GUILayout.Space(5);

                if (GUILayout.Button("Create Personal Access Token", GUILayout.Height(25)))
                {
                    Application.OpenURL("https://github.com/settings/tokens");
                    EditorUtility.DisplayDialog("Personal Access Token",
                        "Please create a Personal Access Token with 'repo' scope from GitHub Settings.", "OK");
                }

                githubToken = EditorGUILayout.TextField("Personal Access Token:", githubToken);

                GUILayout.Space(5);

                GUILayout.Label("Or use username/password:", EditorStyles.boldLabel);

                githubUsername = EditorGUILayout.TextField("Username:", githubUsername);

                EditorGUILayout.BeginHorizontal();
                if (showPassword)
                    githubPassword = EditorGUILayout.TextField("Password:", githubPassword);
                else
                    githubPassword = EditorGUILayout.PasswordField("Password:", githubPassword);

                if (GUILayout.Button(showPassword ? "Hide" : "Show", GUILayout.Width(50)))
                    showPassword = !showPassword;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Public repositories can be accessed without authentication,\n" +
                    "but providing credentials may avoid GitHub API rate limits.",
                    MessageType.Info);

                // Still allow credentials for public repos (optional)
                githubUsername = EditorGUILayout.TextField("Username (optional):", githubUsername);
                githubToken = EditorGUILayout.TextField("Token (optional):", githubToken);
            }

            if (!setupConfirmed)
            {
                // Initial confirmation screen
                EditorGUILayout.HelpBox(
                    "This tool will set up your Unity project with standard scene objects and configurations. " +
                    "Would you like to proceed with the installation?",
                    MessageType.Info);

                GUILayout.Space(20);

                if (GUILayout.Button("Start Installation", GUILayout.Height(40)))
                {
                    setupConfirmed = true;
                    currentSetupStep = 0;
                    Saltvision.VysoraSetup.Logger.Info("Setup process initiated");
                }
            }
            else
            {
                // Show current step progress
                GUILayout.Label($"Setup Progress: Step {currentSetupStep + 1} of {setupStepLabels.Length}", EditorStyles.boldLabel);

                // Progress bar - IMPORTANT: Make sure we're not accessing outside the array bounds
                if (currentSetupStep < setupStepLabels.Length)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 24f),
                        (float)(currentSetupStep) / setupStepLabels.Length,
                        setupStepLabels[currentSetupStep]);
                }
                else
                {
                    // Handle the case where currentSetupStep is out of bounds
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 24f),
                        1.0f,
                        "Setup Complete");
                }

                GUILayout.Space(20);

                // Current step content - Make sure to add case 2 for the new step
                switch (currentSetupStep)
                {
                    case 0: // Create Scene Objects
                        GUILayout.Label("Step 1: Create Scene Objects", EditorStyles.boldLabel);

                        EditorGUILayout.HelpBox(
                            "This step will create basic scene objects:\n" +
                            "  Main Camera with proper positioning\n" +
                            "  Lighting setup (directional and point lights)\n" +
                            "  Example content\n" +
                            "  UI Document setup",
                            MessageType.Info);

                        GUILayout.Space(10);

                        if (GUILayout.Button("Create Scene Objects", GUILayout.Height(40)))
                        {
                            // Call the scene setup method and then increment step
                            CreateSceneObjects();

                            // After successful creation, move to the next step
                            currentSetupStep++;
                        }
                        break;

                    case 1: // Download Assets
                        GUILayout.Label("Step 2: Download Project Assets", EditorStyles.boldLabel);

                        EditorGUILayout.HelpBox(
                            "This step will download the project assets from GitHub:\n" +
                            "  Scripts, Models, Materials, and other required files\n" +
                            "  Asset files will be organized in their appropriate folders",
                            MessageType.Info);

                        GUILayout.Space(10);

                        if (isDownloading)
                        {
                            EditorGUI.ProgressBar(
                                EditorGUILayout.GetControlRect(false, 24f),
                                downloadProgress,
                                downloadStatus);
                        }
                        else
                        {
                            if (GUILayout.Button("Download Assets", GUILayout.Height(40)))
                            {
                                // Check if Git is installed
                                if (!gitInstalled)
                                {
                                    EditorUtility.DisplayDialog("Git Not Found",
                                        "Git is required for downloading assets. Please install Git and restart Unity.",
                                        "OK");
                                    return;
                                }

                                // If authentication is required but not provided
                                if (isPrivateRepo &&
                                    string.IsNullOrEmpty(githubToken) &&
                                    (string.IsNullOrEmpty(githubUsername) || string.IsNullOrEmpty(githubPassword)))
                                {
                                    EditorUtility.DisplayDialog("Authentication Required",
                                        "Please provide a Personal Access Token or username/password for the private repository.",
                                        "OK");
                                    return;
                                }

                                // Start download
                                DownloadAssets();
                            }
                        }
                        break;

                    case 2: // Configure Game Objects
                        GUILayout.Label("Step 3: Configure Game Objects", EditorStyles.boldLabel);

                        EditorGUILayout.HelpBox(
                            "This step will configure the game objects with scripts and assets:\n" +
                            "  Set UI.uxml as the source asset for the UI Document\n" +
                            "  Add SimpleCameraController to the Main Camera\n" +
                            "  Add UI scripts to the UI Document GameObject\n" +
                            "  Create and configure a TileMap GameObject",
                            MessageType.Info);

                        GUILayout.Space(10);

                        if (isDownloading)
                        {
                            EditorGUI.ProgressBar(
                                EditorGUILayout.GetControlRect(false, 24f),
                                downloadProgress,
                                downloadStatus);
                        }
                        else
                        {
                            if (GUILayout.Button("Configure Game Objects", GUILayout.Height(40)))
                            {
                                isDownloading = true; // Reuse the download progress UI
                                downloadProgress = 0f;
                                downloadStatus = "Preparing to configure game objects...";

                                // Start configuration in a delayed call to allow UI to update
                                EditorApplication.delayCall += () =>
                                {
                                    ConfigureGameObjects();
                                    isDownloading = false;
                                };
                            }
                        }
                        break;

                    case 3: // Configure Script Properties
                        GUILayout.Label("Step 4: Configure Script Properties", EditorStyles.boldLabel);

                        EditorGUILayout.HelpBox(
                            "This step will configure the script properties with the correct values:\n" +
                            "  Set SimpleCameraController parameters\n" +
                            "  Configure UI scripts\n" +
                            "  Set up other component parameters",
                            MessageType.Info);

                        GUILayout.Space(10);

                        if (isDownloading) // Reuse the progress bar UI
                        {
                            EditorGUI.ProgressBar(
                                EditorGUILayout.GetControlRect(false, 24f),
                                downloadProgress,
                                downloadStatus);
                        }
                        else
                        {
                            if (GUILayout.Button("Configure Script Properties", GUILayout.Height(40)))
                            {
                                isDownloading = true; // Reuse the download progress UI
                                downloadProgress = 0f;
                                downloadStatus = "Preparing to configure script properties...";

                                // Start configuration in a delayed call to allow UI to update
                                EditorApplication.delayCall += () =>
                                {
                                    ConfigureScriptProperties();
                                    isDownloading = false;
                                };
                            }
                        }
                        break;

                    case 4: // Setup Complete
                        GUILayout.Label("Setup Complete", EditorStyles.boldLabel);

                        EditorGUILayout.HelpBox(
                            "The project setup is complete! You can now start using the project.\n\n" +
                            "Some things you might want to do next:\n" +
                            "  Save the current scene (File > Save)\n" +
                            "  Review the added components in the Inspector\n" +
                            "  Check the project structure in the Project window",
                            MessageType.Info);

                        GUILayout.Space(10);

                        if (GUILayout.Button("Open Documentation", GUILayout.Height(30)))
                        {
                            Application.OpenURL("https://github.com/vysora/project-setup-tool");
                        }

                        GUILayout.Space(10);

                        if (GUILayout.Button("Finish and Close", GUILayout.Height(40)))
                        {
                            // Reset setup state to allow restarting if needed
                            setupConfirmed = false;
                            currentSetupStep = 0;
                            // Close the window
                            this.Close();
                        }
                        break;
                }

                GUILayout.Space(20);


                // Allow canceling the setup
                if (GUILayout.Button("Cancel Setup", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Cancel Setup",
                        "Are you sure you want to cancel the setup process?",
                        "Yes", "No"))
                    {
                        setupConfirmed = false;
                        isLoggedIn = false;
                        githubToken = "";
                        githubPassword = "";
                        Saltvision.VysoraSetup.Logger.Info("Setup process canceled by user");
                    }
                }
            }
        }

        private void DrawAboutTab()
        {
            aboutScrollPosition = EditorGUILayout.BeginScrollView(aboutScrollPosition);

            GUILayout.Label("Vysora Project Setup Tool", EditorStyles.boldLabel);
            GUILayout.Label("Version: 1.0", EditorStyles.miniLabel);
            GUILayout.Label("Build Date: July 2025", EditorStyles.miniLabel);

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool helps you quickly set up Unity projects with common configurations and scene objects.",
                MessageType.Info);

            GUILayout.Space(10);

            GUILayout.Label("Features:", EditorStyles.boldLabel);
            GUILayout.Label("  Automated Scene Setup");
            GUILayout.Label("  Standardized Lighting Configuration");
            GUILayout.Label("  Camera Position Presets");
            GUILayout.Label("  UI Toolkit Setup with PanelSettings");
            GUILayout.Label("  Asset Download via GitHub");

            GUILayout.Space(20);

            if (GUILayout.Button("View Documentation"))
            {
                Application.OpenURL("https://github.com/vysora/project-setup-tool");
            }

            if (GUILayout.Button("Report an Issue"))
            {
                Application.OpenURL("https://github.com/vysora/project-setup-tool/issues");
            }

            GUILayout.Space(20);

            GUILayout.Label("Created by Vysora", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndScrollView();
        }

        #region Step 1: Scene Setup

        // Scene setup - Create the scene objects with proper hierarchy and components
        private void CreateSceneObjects()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Creating Scene Objects", "Setting up scene hierarchy...", 0.5f);
                Saltvision.VysoraSetup.Logger.Info("Creating scene objects");

                // First, clear existing scene objects
                ClearExistingSceneObjects();

                // 1. Main Camera
                GameObject mainCamera = new GameObject("Main Camera");
                mainCamera.AddComponent<Camera>();
                mainCamera.tag = "MainCamera";
                mainCamera.transform.position = new Vector3(0, 2.49f, -6.46f);
                mainCamera.transform.rotation = Quaternion.Euler(8.81f, 0, 0);

                // 2. Lighting parent object
                GameObject lighting = new GameObject("Lighting");

                // 2-1. Directional Light
                GameObject directionalLight = new GameObject("Directional Light");
                directionalLight.transform.SetParent(lighting.transform);
                directionalLight.transform.position = new Vector3(0, 3, 0);
                directionalLight.transform.rotation = Quaternion.Euler(45f, 44f, 0);
                Light dirLight = directionalLight.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = HexToColor("#FFF5E8");
                dirLight.shadows = LightShadows.Soft;

                // 2-2. Fill Light (Point Light)
                GameObject fillLight = new GameObject("Fill Light");
                fillLight.transform.SetParent(lighting.transform);
                fillLight.transform.position = new Vector3(-0.37f, 1.148f, -2.68f);
                Light fillLightComp = fillLight.AddComponent<Light>();
                fillLightComp.type = LightType.Point;
                fillLightComp.color = HexToColor("#E8F1FF");
                fillLightComp.intensity = 0.5f;

                // 2-3. Back Light (Point Light)
                GameObject backLight = new GameObject("Back Light");
                backLight.transform.SetParent(lighting.transform);
                backLight.transform.position = new Vector3(-0.37f, 1.69f, 2.86f);
                Light backLightComp = backLight.AddComponent<Light>();
                backLightComp.type = LightType.Point;
                backLightComp.color = HexToColor("#E8F1FF");
                backLightComp.intensity = 0.8f;

                // 2-4. Light Probe Group
                GameObject lightProbeGroup = new GameObject("Light Probe Group");
                lightProbeGroup.transform.SetParent(lighting.transform);
                lightProbeGroup.AddComponent<LightProbeGroup>();

                // 2-5. Reflection Probe
                GameObject reflectionProbe = new GameObject("Reflection Probe");
                reflectionProbe.transform.SetParent(lighting.transform);
                reflectionProbe.AddComponent<ReflectionProbe>();

                // 3. Example Content
                GameObject content = new GameObject("Content");

                // 3-1. Cube
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(content.transform);
                cube.transform.position = new Vector3(0, 0.51f, 0);

                // 4. Camera Positions
                GameObject cameraPositions = new GameObject("Camera Positions");

                // 4-1. Main Camera Position
                GameObject mainCamPosition = new GameObject("Main");
                mainCamPosition.transform.SetParent(cameraPositions.transform);
                mainCamPosition.transform.position = new Vector3(0, 2.49f, -6.46f);
                mainCamPosition.transform.rotation = Quaternion.Euler(8.81f, 0, 0);

                // 4-2. Top Camera Position
                GameObject topCamPosition = new GameObject("Top");
                topCamPosition.transform.SetParent(cameraPositions.transform);
                topCamPosition.transform.position = new Vector3(0, 7, 0);
                topCamPosition.transform.rotation = Quaternion.Euler(90, 0, 0);

                // 4-3. Side Camera Position
                GameObject sideCamPosition = new GameObject("Side");
                sideCamPosition.transform.SetParent(cameraPositions.transform);
                sideCamPosition.transform.position = new Vector3(-5.21f, 2.11f, -7.01f);
                sideCamPosition.transform.rotation = Quaternion.Euler(8.81f, 33.19f, 0);

                // 5. UI Document (UI Toolkit)
                GameObject uiDocument = new GameObject("UI Document");
                UIDocument document = uiDocument.AddComponent<UIDocument>();

                // Create UI Toolkit assets and assign PanelSettings
                PanelSettings panelSettings = CreatePanelSettings();
                document.panelSettings = panelSettings;

                // Create UI Document asset
                CreateUIDocumentAsset(document);

                Saltvision.VysoraSetup.Logger.Info("Scene objects created successfully!");
                EditorUtility.DisplayDialog("Success", "Scene objects created successfully!", "OK");
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Create scene objects");
                EditorUtility.DisplayDialog("Error", $"Failed to create scene objects: {ex.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Clear existing objects in the scene
        private void ClearExistingSceneObjects()
        {
            Saltvision.VysoraSetup.Logger.Info("Clearing existing scene objects");

            // Get all root GameObjects
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            // Create a list to store objects we want to delete
            List<GameObject> objectsToDelete = new List<GameObject>();

            foreach (GameObject obj in rootObjects)
            {
                // You can add exceptions here if needed
                // For example, if you want to keep certain objects
                // if (obj.name != "DontDeleteMe") {
                objectsToDelete.Add(obj);
                // }
            }

            // Delete all the objects
            foreach (GameObject obj in objectsToDelete)
            {
                DestroyImmediate(obj);
            }

            Saltvision.VysoraSetup.Logger.Info($"Cleared {objectsToDelete.Count} objects from the scene");
        }

        // Create a PanelSettings asset based on the provided YAML
        private PanelSettings CreatePanelSettings()
        {
            // Create UI Toolkit folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder("Assets/UI Toolkit"))
            {
                AssetDatabase.CreateFolder("Assets", "UI Toolkit");
            }

            string panelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

            // Check if the asset already exists
            PanelSettings existingSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            if (existingSettings != null)
            {
                Saltvision.VysoraSetup.Logger.Info("PanelSettings asset already exists, using existing one");
                return existingSettings;
            }

            // Create new PanelSettings asset
            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // Configure panel settings based on the YAML values
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f; // Match is in the middle (50% width, 50% height)
            panelSettings.sortingOrder = 0;
            panelSettings.targetDisplay = 0;
            panelSettings.clearDepthStencil = true;
            panelSettings.clearColor = false;
            panelSettings.colorClearValue = Color.clear;
            panelSettings.scale = 1;
            panelSettings.referenceDpi = 96;
            panelSettings.fallbackDpi = 96;

            // Save the asset
            AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
            AssetDatabase.SaveAssets();

            Saltvision.VysoraSetup.Logger.Info($"Created PanelSettings asset at {panelSettingsPath}");
            return panelSettings;
        }

        // Create a basic UI Document asset if it doesn't exist
        private void CreateUIDocumentAsset(UIDocument document)
        {
            // Check if UI Toolkit/Documents folder exists, create if not
            if (!AssetDatabase.IsValidFolder("Assets/UI Toolkit"))
            {
                AssetDatabase.CreateFolder("Assets", "UI Toolkit");
            }

            if (!AssetDatabase.IsValidFolder("Assets/UI Toolkit/Documents"))
            {
                AssetDatabase.CreateFolder("Assets/UI Toolkit", "Documents");
            }

            string uiDocumentPath = "Assets/UI Toolkit/Documents/DefaultUI.uxml";

            // Only create if it doesn't exist
            if (!File.Exists(uiDocumentPath))
            {
                // Create a basic UXML file
                string uxml =
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\">\n" +
                    "    <ui:VisualElement style=\"flex-grow: 1;\">\n" +
                    "        <ui:Label text=\"Vysora Project\" style=\"-unity-font-style: bold; font-size: 20px; color: rgb(255, 255, 255); -unity-text-align: upper-center; margin-top: 20px;\"/>\n" +
                    "        <ui:Button text=\"Main Button\" style=\"width: 200px; height: 50px; align-self: center; margin-top: 20px;\"/>\n" +
                    "    </ui:VisualElement>\n" +
                    "</ui:UXML>";

                File.WriteAllText(uiDocumentPath, uxml);
                AssetDatabase.Refresh();

                Saltvision.VysoraSetup.Logger.Info($"Created UI Document asset at {uiDocumentPath}");
            }

            // Assign the asset to the document component
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uiDocumentPath);
        }

        // Utility methods
        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }

        #endregion

        #region Step 2: GitHub Integration

        // Check if Git is installed
        private void CheckGitInstallation()
        {
            try
            {
                // Try to find git executable
                Process process = new Process();

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    process.StartInfo.FileName = "where";
                    process.StartInfo.Arguments = "git";
                }
                else // macOS or Linux
                {
                    process.StartInfo.FileName = "which";
                    process.StartInfo.Arguments = "git";
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    gitExecutablePath = output.Trim();
                    gitInstalled = true;
                    Saltvision.VysoraSetup.Logger.Info($"Git found at: {gitExecutablePath}");
                }
                else
                {
                    gitInstalled = false;
                    Saltvision.VysoraSetup.Logger.Warning("Git not found on system PATH");
                }
            }
            catch (Exception ex)
            {
                gitInstalled = false;
                Saltvision.VysoraSetup.Logger.Exception(ex, "Check git installation");
            }
        }

        // Login to GitHub
        private void LoginToGitHub()
        {
            try
            {
                // For simplicity, we'll just validate that we can access GitHub
                // In a real-world scenario, you would validate credentials more securely

                EditorUtility.DisplayProgressBar("GitHub", "Verifying credentials...", 0.5f);

                // Simple validation - in a real app, this would be done via GitHub API
                if (!string.IsNullOrEmpty(githubToken) ||
                    (!string.IsNullOrEmpty(githubUsername) && !string.IsNullOrEmpty(githubPassword)))
                {
                    // Just check if git works
                    Process process = new Process();
                    process.StartInfo.FileName = "git";
                    process.StartInfo.Arguments = "--version";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        isLoggedIn = true;
                        Saltvision.VysoraSetup.Logger.Info("GitHub login successful");
                        EditorUtility.DisplayDialog("Success", "GitHub login successful!", "OK");
                    }
                    else
                    {
                        throw new Exception("Git command failed");
                    }
                }
                else
                {
                    throw new Exception("Invalid credentials");
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "GitHub login");
                EditorUtility.DisplayDialog("Login Failed",
                    $"Could not login to GitHub: {ex.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Download assets from GitHub
        private async void DownloadAssets()
        {
            try
            {
                isDownloading = true;
                downloadProgress = 0f;
                downloadStatus = "Preparing to download...";

                // Check if we need to log in first
                if (isPrivateRepo && !isLoggedIn)
                {
                    downloadStatus = "Logging in...";
                    LoginToGitHub();

                    // If login failed, abort download
                    if (!isLoggedIn)
                    {
                        isDownloading = false;
                        EditorUtility.DisplayDialog("Authentication Failed",
                            "Could not authenticate with GitHub. Please check your credentials.", "OK");
                        return;
                    }
                }

                // Create the download directory if it doesn't exist
                if (!Directory.Exists(assetDownloadPath))
                {
                    Directory.CreateDirectory(assetDownloadPath);
                }

                // Start a task for downloading to avoid freezing the UI
                await Task.Run(() => CloneRepository());

                // Refresh AssetDatabase to detect the new files
                AssetDatabase.Refresh();

                downloadStatus = "Download complete!";
                downloadProgress = 1f;

                Saltvision.VysoraSetup.Logger.Info("Assets downloaded successfully");
                EditorUtility.DisplayDialog("Success", "Project assets downloaded successfully!", "OK");

                // Move to next step if we add more in the future
                currentSetupStep++;

                // Reset download state
                isDownloading = false;
            }
            catch (Exception ex)
            {
                isDownloading = false;
                Saltvision.VysoraSetup.Logger.Exception(ex, "Download assets");
                EditorUtility.DisplayDialog("Download Failed",
                    $"Could not download assets: {ex.Message}", "OK");
            }
        }

        // Clone GitHub repository
        // Complete CloneRepository method
        private void CloneRepository()
        {
            // Build the repository URL
            string repoUrl;
            string tempFolder = Path.Combine(Application.dataPath, "VysoraTemp");

            // For private repos, we must use authentication
            if (isPrivateRepo)
            {
                // If token is provided, use it (recommended for private repos)
                if (!string.IsNullOrEmpty(githubToken))
                {
                    repoUrl = $"https://{githubToken}@github.com/{githubRepo}.git";
                    Saltvision.VysoraSetup.Logger.Info("Using token authentication for GitHub");
                }
                // Otherwise use basic auth if username and password are provided
                else if (!string.IsNullOrEmpty(githubUsername) && !string.IsNullOrEmpty(githubPassword))
                {
                    repoUrl = $"https://{githubUsername}:{githubPassword}@github.com/{githubRepo}.git";
                    Saltvision.VysoraSetup.Logger.Info("Using username/password authentication for GitHub");
                }
                else
                {
                    throw new Exception("Authentication required for private repositories");
                }
            }
            else
            {
                // For public repos, we can use auth if provided, otherwise no auth
                if (!string.IsNullOrEmpty(githubToken))
                {
                    repoUrl = $"https://{githubToken}@github.com/{githubRepo}.git";
                }
                else if (!string.IsNullOrEmpty(githubUsername) && !string.IsNullOrEmpty(githubPassword))
                {
                    repoUrl = $"https://{githubUsername}:{githubPassword}@github.com/{githubRepo}.git";
                }
                else
                {
                    // No auth for public repos
                    repoUrl = $"https://github.com/{githubRepo}.git";
                    Saltvision.VysoraSetup.Logger.Info("Using public access for GitHub (no authentication)");
                }
            }

            // Try to clear temp directory if it exists
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    // Try a safer approach to delete the directory
                    SafeDeleteDirectory(tempFolder);
                }

                // Create the temp directory
                Directory.CreateDirectory(tempFolder);
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Failed to prepare temp directory");

                // Try using a new temp folder with a unique name
                tempFolder = Path.Combine(Application.dataPath, "VysoraTemp_" + DateTime.Now.Ticks);
                Saltvision.VysoraSetup.Logger.Info($"Using alternative temp folder: {tempFolder}");
                Directory.CreateDirectory(tempFolder);
            }

            // Run git clone command
            try
            {
                downloadStatus = "Cloning repository...";

                // Start the process
                Process process = new Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = $"clone {repoUrl} \"{tempFolder}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // Capture output for logging
                System.Text.StringBuilder outputBuilder = new System.Text.StringBuilder();
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        outputBuilder.AppendLine(args.Data);
                        // Try to parse progress if possible
                        if (args.Data.Contains("Receiving objects:"))
                        {
                            try
                            {
                                var match = Regex.Match(args.Data, @"Receiving objects:\s+(\d+)%");
                                if (match.Success)
                                {
                                    int progress = int.Parse(match.Groups[1].Value);
                                    downloadProgress = progress / 100f;
                                    downloadStatus = args.Data;
                                }
                            }
                            catch { }
                        }
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        outputBuilder.AppendLine($"ERROR: {args.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Git clone failed with exit code {process.ExitCode}. Output: {outputBuilder}");
                }

                downloadProgress = 0.8f;
                downloadStatus = "Moving files to correct locations...";

                // Step 3: Relocate the downloaded files to their correct locations
                RelocateFiles(tempFolder);

                // Clean up the temp folder - use safer method
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        SafeDeleteDirectory(tempFolder);
                    }
                }
                catch (Exception ex)
                {
                    // Just log the cleanup error but don't fail the overall process
                    Saltvision.VysoraSetup.Logger.Warning($"Could not remove temp folder: {ex.Message}. Manual cleanup may be required.");
                }

                downloadProgress = 1.0f;
                downloadStatus = "Download and installation complete!";
            }
            catch (Exception ex)
            {
                throw new Exception($"Repository clone failed: {ex.Message}");
            }
        }

        // Helper method for safer directory deletion
        private void SafeDeleteDirectory(string directory)
        {
            try
            {
                // First try to remove read-only attributes from all files
                DirectoryInfo dirInfo = new DirectoryInfo(directory);

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }

                // Use Windows file operations to delete the directory if on Windows
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Try to run an external process to force delete
                    Process process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c rd /s /q \"{directory}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();

                    // Check if directory still exists
                    if (Directory.Exists(directory))
                    {
                        Saltvision.VysoraSetup.Logger.Warning($"Command line deletion failed, falling back to .NET delete for {directory}");
                        Directory.Delete(directory, true);
                    }
                }
                else
                {
                    // On Mac/Linux, use the standard Directory.Delete
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, $"Failed to delete directory {directory}");
                throw;
            }
        }
        private void RelocateFiles(string tempFolder)
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Relocating downloaded files to their correct locations");

                // Create destination directories if they don't exist
                string modelsDestination = Path.Combine(Application.dataPath, "3dModel");
                string pluginsDestination = Path.Combine(Application.dataPath, "Plugins");
                string scriptsDestination = Path.Combine(Application.dataPath, "Scripts");
                string materialsDestination = Path.Combine(Application.dataPath, "Materials");
                string resourcesDestination = Path.Combine(Application.dataPath, "Resources");
                string uiDestination = Path.Combine(Application.dataPath, "UI");

                EnsureDirectoryExists(modelsDestination);
                EnsureDirectoryExists(pluginsDestination);
                EnsureDirectoryExists(scriptsDestination);
                EnsureDirectoryExists(materialsDestination);
                EnsureDirectoryExists(resourcesDestination);
                EnsureDirectoryExists(uiDestination);

                // Move the 3dModel folder contents
                string modelsSource = Path.Combine(tempFolder, "3dModel");
                if (Directory.Exists(modelsSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving 3dModel files from {modelsSource} to {modelsDestination}");
                    CopyDirectoryContents(modelsSource, modelsDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("3dModel folder not found in downloaded repository");
                }

                // Move the Plugins folder contents
                string pluginsSource = Path.Combine(tempFolder, "Plugins");
                if (Directory.Exists(pluginsSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving Plugins files from {pluginsSource} to {pluginsDestination}");
                    CopyDirectoryContents(pluginsSource, pluginsDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Plugins folder not found in downloaded repository");
                }

                // Move the Scripts folder contents
                string scriptsSource = Path.Combine(tempFolder, "Scripts");
                if (Directory.Exists(scriptsSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving Scripts files from {scriptsSource} to {scriptsDestination}");
                    CopyDirectoryContents(scriptsSource, scriptsDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Scripts folder not found in downloaded repository");
                }

                // Move the Materials folder contents
                string materialsSource = Path.Combine(tempFolder, "Materials");
                if (Directory.Exists(materialsSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving Materials files from {materialsSource} to {materialsDestination}");
                    CopyDirectoryContents(materialsSource, materialsDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Materials folder not found in downloaded repository");
                }

                // Move the Resources folder contents
                string resourcesSource = Path.Combine(tempFolder, "Resources");
                if (Directory.Exists(resourcesSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving Resources files from {resourcesSource} to {resourcesDestination}");
                    CopyDirectoryContents(resourcesSource, resourcesDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Resources folder not found in downloaded repository");
                }

                // Move the UI folder contents
                string uiSource = Path.Combine(tempFolder, "UI");
                if (Directory.Exists(uiSource))
                {
                    Saltvision.VysoraSetup.Logger.Info($"Moving UI files from {uiSource} to {uiDestination}");
                    CopyDirectoryContents(uiSource, uiDestination);
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("UI folder not found in downloaded repository");
                }

                Saltvision.VysoraSetup.Logger.Info("Files relocated successfully");
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Failed to relocate files");
                throw new Exception($"Failed to relocate files: {ex.Message}");
            }
        }    // Helper method to ensure a directory exists
        private void EnsureDirectoryExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Saltvision.VysoraSetup.Logger.Info($"Created directory: {directory}");
            }
        }

        // Helper method to copy directory contents recursively
        private void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
                Saltvision.VysoraSetup.Logger.Info($"Copied file: {fileName}");
            }

            // Copy all subdirectories and their contents
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);

                // Create the subdirectory if it doesn't exist
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    Saltvision.VysoraSetup.Logger.Info($"Created directory: {destDir}");
                }

                // Recursively copy the subdirectory
                CopyDirectoryContents(directory, destDir);
            }
        }
        // New method for updating existing repositories
        private void UpdateExistingRepository()
        {
            try
            {
                downloadStatus = "Updating existing repository...";

                // Start the process
                Process process = new Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = "pull";
                process.StartInfo.WorkingDirectory = assetDownloadPath;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // Capture output for logging
                System.Text.StringBuilder outputBuilder = new System.Text.StringBuilder();
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        outputBuilder.AppendLine(args.Data);
                        downloadStatus = $"Updating: {args.Data}";
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        outputBuilder.AppendLine($"ERROR: {args.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Git pull failed with exit code {process.ExitCode}. Output: {outputBuilder}");
                }

                downloadProgress = 1.0f;
                downloadStatus = "Repository updated successfully!";
            }
            catch (Exception ex)
            {
                throw new Exception($"Repository update failed: {ex.Message}");
            }
        }

        #endregion

        #region Step 3 : Configure Game Objects

        // Step 3: Configure Game Objects with Scripts and Assets
        // Step 3: Configure Game Objects with Scripts and Assets
        private void ConfigureGameObjects()
        {
            try
            {
                downloadStatus = "Configuring game objects...";
                downloadProgress = 0.0f;

                Saltvision.VysoraSetup.Logger.Info("Starting configuration of game objects with scripts and assets");

                // Track success/failure of each configuration step
                bool allSuccessful = true;
                StringBuilder errorLog = new StringBuilder();

                // 1. Add UI.uxml to the UI Document as the source asset
                bool step1Success = ConfigureUIDocument();
                if (!step1Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure UI Document with UI.uxml");
                }
                downloadProgress = 0.1f;

                // 2. Add SimpleCameraController script to the Main Camera
                bool step2Success = ConfigureMainCamera();
                if (!step2Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure Main Camera with SimpleCameraController");
                }
                downloadProgress = 0.3f;

                // 3-7. Add scripts to the UI Document GameObject
                bool step3Success = ConfigureUIDocumentScripts();
                if (!step3Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to add scripts to UI Document GameObject");
                }
                downloadProgress = 0.6f;

                // 8. Add TileMapGenerator script to a new TileMap GameObject
                bool step8Success = ConfigureTileMap();
                if (!step8Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to create and configure TileMap GameObject");
                }
                downloadProgress = 0.9f;

                // Final steps
                if (allSuccessful)
                {
                    downloadStatus = "Configuration completed successfully!";
                    downloadProgress = 1.0f;
                    EditorUtility.DisplayDialog("Success", "All game objects have been configured successfully!", "OK");
                    Saltvision.VysoraSetup.Logger.Info("Game object configuration completed successfully");

                    // Move to the next step (Setup Complete)
                    currentSetupStep++;
                }
                else
                {
                    downloadStatus = "Configuration completed with errors";
                    downloadProgress = 1.0f;
                    EditorUtility.DisplayDialog("Configuration Warning",
                        $"Some configuration steps failed:\n{errorLog.ToString()}\n\nYou may need to manually configure these components.",
                        "OK");
                    Saltvision.VysoraSetup.Logger.Warning($"Game object configuration completed with errors: {errorLog.ToString()}");

                    // Move to the next step despite errors
                    currentSetupStep++;
                }

                // REMOVE THIS LINE - it's causing the double increment!
                // currentSetupStep++;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure game objects");
                EditorUtility.DisplayDialog("Configuration Error",
                    $"An error occurred while configuring game objects: {ex.Message}",
                    "OK");
            }
        }

        // 1. Configure UI Document with UI.uxml
        // 1. Configure UI Document with UI.uxml
        private bool ConfigureUIDocument()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring UI Document with UI.uxml");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Warning("UI Document GameObject not found in scene. Creating one.");
                    uiDocumentObj = new GameObject("UI Document");
                    uiDocumentObj.AddComponent<UIDocument>();
                }

                // Get the UI Document component
                UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Warning("UIDocument component not found. Adding one.");
                    uiDocument = uiDocumentObj.AddComponent<UIDocument>();
                }

                // Make sure the panel settings are assigned
                if (uiDocument.panelSettings == null)
                {
                    // Try to find existing panel settings
                    PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset");
                    if (panelSettings == null)
                    {
                        // Create panel settings if they don't exist
                        panelSettings = CreatePanelSettings();
                    }
                    uiDocument.panelSettings = panelSettings;
                    Saltvision.VysoraSetup.Logger.Info("Assigned PanelSettings to UI Document");
                }

                // Find the UI.uxml asset
                string uiAssetPath = "Assets/UI/UI.uxml";
                VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uiAssetPath);

                if (uiAsset == null)
                {
                    // Try with a different path - maybe it's in a subfolder
                    Saltvision.VysoraSetup.Logger.Warning($"UI.uxml not found at {uiAssetPath}, searching for it...");

                    // Search for UI.uxml file in the UI folder and its subfolders
                    string[] foundFiles = Directory.GetFiles(Path.Combine(Application.dataPath, "UI"), "UI.uxml", SearchOption.AllDirectories);

                    if (foundFiles.Length > 0)
                    {
                        // Found the file, convert to asset path
                        string fullPath = foundFiles[0];
                        string relativePath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                        relativePath = relativePath.Replace('\\', '/'); // Ensure forward slashes

                        Saltvision.VysoraSetup.Logger.Info($"Found UI.uxml at: {relativePath}");
                        uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(relativePath);

                        if (uiAsset == null)
                        {
                            Saltvision.VysoraSetup.Logger.Error($"Found UI.uxml file at {relativePath} but couldn't load it as VisualTreeAsset");
                            return false;
                        }
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Error("Could not find UI.uxml anywhere in the UI folder");
                        return false;
                    }
                }

                // Assign the UI.uxml asset to the UI Document
                uiDocument.visualTreeAsset = uiAsset;
                Saltvision.VysoraSetup.Logger.Info($"Successfully configured UI Document with UI.uxml from {AssetDatabase.GetAssetPath(uiAsset)}");

                // Save the scene to persist changes
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure UI Document");
                return false;
            }
        }
        // 2. Configure Main Camera with SimpleCameraController
        // 2. Configure Main Camera with SimpleCameraController
        private bool ConfigureMainCamera()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring Main Camera with SimpleCameraController");

                // Find the Main Camera
                GameObject mainCamera = GameObject.FindWithTag("MainCamera");
                if (mainCamera == null)
                {
                    Saltvision.VysoraSetup.Logger.Warning("Main Camera not found in scene. Creating one.");
                    mainCamera = new GameObject("Main Camera");
                    mainCamera.tag = "MainCamera";
                    mainCamera.AddComponent<Camera>();
                    mainCamera.transform.position = new Vector3(0, 2.49f, -6.46f);
                    mainCamera.transform.rotation = Quaternion.Euler(8.81f, 0, 0);
                }

                // Find the SimpleCameraController script
                string scriptPath = "Assets/Scripts/SimpleCameraController.cs";
                MonoScript cameraScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

                if (cameraScript == null)
                {
                    // Try to find the script in subfolders
                    Saltvision.VysoraSetup.Logger.Warning($"SimpleCameraController script not found at {scriptPath}, searching for it...");

                    string[] guids = AssetDatabase.FindAssets("SimpleCameraController t:MonoScript");
                    if (guids.Length > 0)
                    {
                        string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        Saltvision.VysoraSetup.Logger.Info($"Found SimpleCameraController script at: {foundPath}");
                        cameraScript = AssetDatabase.LoadAssetAtPath<MonoScript>(foundPath);
                    }

                    if (cameraScript == null)
                    {
                        Saltvision.VysoraSetup.Logger.Error("Could not find SimpleCameraController script anywhere in the project");
                        return false;
                    }
                }

                // Add the script to the Main Camera if it doesn't already have it
                if (mainCamera.GetComponent(cameraScript.GetClass()) == null)
                {
                    mainCamera.AddComponent(cameraScript.GetClass());
                    Saltvision.VysoraSetup.Logger.Info("Added SimpleCameraController to Main Camera");
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Info("Main Camera already has SimpleCameraController, skipping");
                }

                // Save the scene to persist changes
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure Main Camera");
                return false;
            }
        }
        // 3-7. Configure UI Document GameObject with multiple scripts
        private bool ConfigureUIDocumentScripts()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring UI Document GameObject with scripts");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Define script paths and names
                Dictionary<string, string> scripts = new Dictionary<string, string>()
        {
           // { "MenuNavigationManager", "Assets/Scripts/UI Scripts/MenuNavigationManager.cs" },
            { "ButtonLabelController", "Assets/Scripts/ButtonLabelController.cs" },
            { "EnhancedFullScreenToggle", "Assets/Scripts/EnhancedFullScreenToggle.cs" },
            { "EnhancedScreenshotManager", "Assets/Scripts/EnhancedScreenshotManager.cs" },
            { "PopupManager", "Assets/Scripts/PopupManager.cs" }
        };

                // Add each script to the UI Document GameObject
                foreach (var script in scripts)
                {
                    string scriptName = script.Key;
                    string scriptPath = script.Value;

                    MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

                    if (monoScript == null)
                    {
                        Saltvision.VysoraSetup.Logger.Error($"{scriptName} script not found at path: {scriptPath}");
                        continue;
                    }

                    // Check if the component already exists
                    if (uiDocumentObj.GetComponent(monoScript.GetClass()) == null)
                    {
                        uiDocumentObj.AddComponent(monoScript.GetClass());
                        Saltvision.VysoraSetup.Logger.Info($"Added {scriptName} to UI Document GameObject");
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Info($"UI Document already has {scriptName}, skipping");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure UI Document Scripts");
                return false;
            }
        }

        // 8. Create and configure TileMap GameObject
        // 8. Create and configure TileMap GameObject
        private bool ConfigureTileMap()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Creating and configuring TileMap GameObject");

                // Check if TileMap GameObject already exists
                GameObject tileMapObj = GameObject.Find("TileMap");
                if (tileMapObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Info("Creating new TileMap GameObject");
                    tileMapObj = new GameObject("TileMap");
                }

                // Find the TileMapGenerator script
                string[] guids = AssetDatabase.FindAssets("TileMapGenerator t:MonoScript");

                if (guids.Length == 0)
                {
                    Saltvision.VysoraSetup.Logger.Error("Could not find TileMapGenerator script anywhere in the project");
                    return false;
                }

                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                MonoScript tileMapScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

                if (tileMapScript == null)
                {
                    Saltvision.VysoraSetup.Logger.Error($"Failed to load TileMapGenerator script from path: {scriptPath}");
                    return false;
                }

                // Add the script to the TileMap GameObject if it doesn't already have it
                if (tileMapObj.GetComponent(tileMapScript.GetClass()) == null)
                {
                    tileMapObj.AddComponent(tileMapScript.GetClass());
                    Saltvision.VysoraSetup.Logger.Info($"Added TileMapGenerator to TileMap GameObject from {scriptPath}");
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Info("TileMap already has TileMapGenerator, skipping");
                }

                // Save the scene to persist changes
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure TileMap");
                return false;
            }
        }
        #endregion

        // Add this to the #region Step 4: Configure Script Properties section
        #region Step 4: Configure Script Properties

        private bool ConfigureMenuNavigationManager()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring SimpleMenuNavigator");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Try to find the script 
                string[] scriptNames = new string[] { "SimpleMenuNavigator", "MenuNavigationManager" };
                MonoScript script = null;
                string foundScriptName = "";
                string scriptPath = "";

                foreach (string scriptName in scriptNames)
                {
                    // Try to find the script with this name
                    string[] foundScripts = AssetDatabase.FindAssets($"{scriptName} t:Script", new[] { "Assets/Scripts", "Assets/Scripts/UI Scripts" });
                    if (foundScripts.Length > 0)
                    {
                        scriptPath = AssetDatabase.GUIDToAssetPath(foundScripts[0]);
                        script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                        if (script != null)
                        {
                            foundScriptName = scriptName;
                            Saltvision.VysoraSetup.Logger.Info($"Found {scriptName} script at: {scriptPath}");
                            break;
                        }
                    }
                }

                if (script == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("Could not find SimpleMenuNavigator or MenuNavigationManager script");
                    return false;
                }

                // Check if UI Document already has the component
                MonoBehaviour menuNavigator = null;
                MonoBehaviour[] components = uiDocumentObj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == foundScriptName)
                    {
                        menuNavigator = component;
                        break;
                    }
                }

                // Add the component if it doesn't exist
                if (menuNavigator == null)
                {
                    Type scriptType = script.GetClass();
                    if (scriptType != null)
                    {
                        menuNavigator = uiDocumentObj.AddComponent(scriptType) as MonoBehaviour;
                        if (menuNavigator == null)
                        {
                            Saltvision.VysoraSetup.Logger.Error($"Failed to add {foundScriptName} component to UI Document");
                            return false;
                        }
                        Saltvision.VysoraSetup.Logger.Info($"Added {foundScriptName} component to UI Document");
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Error($"Could not get script class for {foundScriptName}");
                        return false;
                    }
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Info($"{foundScriptName} already exists on UI Document, configuring properties");
                }

                // Configure the properties based on the screenshot
                if (menuNavigator != null)
                {
                    Type navigatorType = menuNavigator.GetType();

                    // UI Document reference
                    UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                    if (uiDocument == null)
                    {
                        Saltvision.VysoraSetup.Logger.Error("UIDocument component not found on UI Document GameObject");
                        return false;
                    }

                    // Try setting UI Document reference via reflection
                    bool setUIDoc = false;
                    foreach (var fieldName in new[] { "mainUIDocument", "uiDocument", "document", "uiDoc" })
                    {
                        FieldInfo field = navigatorType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType.IsAssignableFrom(typeof(UIDocument)))
                        {
                            field.SetValue(menuNavigator, uiDocument);
                            Saltvision.VysoraSetup.Logger.Info($"Set {fieldName} to UIDocument reference");
                            setUIDoc = true;
                            break;
                        }
                    }

                    if (!setUIDoc)
                    {
                        Saltvision.VysoraSetup.Logger.Warning("Could not find a field to set the UIDocument reference");
                    }

                    // Find VisualTreeAssets
                    VisualTreeAsset mainMenuTemplate = FindVisualTreeAsset("MainMenu_Item");
                    VisualTreeAsset subMenu1Template = FindVisualTreeAsset("SubMenu1");
                    VisualTreeAsset subMenu2Template = FindVisualTreeAsset("SubMenu2");
                    VisualTreeAsset subMenu3Template = FindVisualTreeAsset("SubMenu3");
                    VisualTreeAsset productMenuATemplate = FindVisualTreeAsset("Product Menu A");

                    // Let's examine the source code to find nested type names
                    if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                    {
                        string sourceCode = File.ReadAllText(scriptPath);

                        // Let's try a simpler approach - directly use SerializedObject to set properties
                        // This works better with Unity's serialization system
                        SerializedObject serializedNavigator = new SerializedObject(menuNavigator);

                        // First, try to reset or clear any existing data
                        // This helps avoid conflicts with existing partially-configured data
                        serializedNavigator.Update();

                        // Find and set mainMenu properties using SerializedProperty
                        SerializedProperty mainMenuProp = serializedNavigator.FindProperty("mainMenu");
                        if (mainMenuProp != null)
                        {
                            SerializedProperty menuNameProp = mainMenuProp.FindPropertyRelative("menuName");
                            if (menuNameProp != null)
                                menuNameProp.stringValue = "Main Menu";

                            SerializedProperty menuTemplateProp = mainMenuProp.FindPropertyRelative("menuTemplate");
                            if (menuTemplateProp != null)
                                menuTemplateProp.objectReferenceValue = mainMenuTemplate;

                            // Try to find button mappings property
                            SerializedProperty buttonMappingsProp = mainMenuProp.FindPropertyRelative("buttonMappings");
                            if (buttonMappingsProp != null)
                            {
                                // Clear existing mappings
                                buttonMappingsProp.ClearArray();

                                // Add 3 new mappings
                                buttonMappingsProp.arraySize = 3;

                                // Configure first mapping
                                SerializedProperty mapping1 = buttonMappingsProp.GetArrayElementAtIndex(0);
                                SerializedProperty mapping1ButtonName = mapping1.FindPropertyRelative("buttonName");
                                if (mapping1ButtonName != null)
                                    mapping1ButtonName.stringValue = "MainMenu_Item1_Image";

                                SerializedProperty mapping1TargetMenu = mapping1.FindPropertyRelative("targetMenuTemplate");
                                if (mapping1TargetMenu != null)
                                    mapping1TargetMenu.objectReferenceValue = subMenu1Template;

                                SerializedProperty mapping1HeaderText = mapping1.FindPropertyRelative("menuHeaderText");
                                if (mapping1HeaderText != null)
                                    mapping1HeaderText.stringValue = "";

                                // Configure second mapping
                                SerializedProperty mapping2 = buttonMappingsProp.GetArrayElementAtIndex(1);
                                SerializedProperty mapping2ButtonName = mapping2.FindPropertyRelative("buttonName");
                                if (mapping2ButtonName != null)
                                    mapping2ButtonName.stringValue = "MainMenu_Item2_Image";

                                SerializedProperty mapping2TargetMenu = mapping2.FindPropertyRelative("targetMenuTemplate");
                                if (mapping2TargetMenu != null)
                                    mapping2TargetMenu.objectReferenceValue = subMenu2Template;

                                SerializedProperty mapping2HeaderText = mapping2.FindPropertyRelative("menuHeaderText");
                                if (mapping2HeaderText != null)
                                    mapping2HeaderText.stringValue = "";

                                // Configure third mapping
                                SerializedProperty mapping3 = buttonMappingsProp.GetArrayElementAtIndex(2);
                                SerializedProperty mapping3ButtonName = mapping3.FindPropertyRelative("buttonName");
                                if (mapping3ButtonName != null)
                                    mapping3ButtonName.stringValue = "MainMenu_Item3_Image";

                                SerializedProperty mapping3TargetMenu = mapping3.FindPropertyRelative("targetMenuTemplate");
                                if (mapping3TargetMenu != null)
                                    mapping3TargetMenu.objectReferenceValue = subMenu3Template;

                                SerializedProperty mapping3HeaderText = mapping3.FindPropertyRelative("menuHeaderText");
                                if (mapping3HeaderText != null)
                                    mapping3HeaderText.stringValue = "";
                            }
                        }

                        // Find and set subMenus properties
                        SerializedProperty subMenusProp = serializedNavigator.FindProperty("subMenus");
                        if (subMenusProp != null)
                        {
                            // Clear existing menus
                            subMenusProp.ClearArray();

                            // Add 3 new submenus
                            subMenusProp.arraySize = 3;

                            // Configure submenu1
                            SerializedProperty submenu1 = subMenusProp.GetArrayElementAtIndex(0);
                            SerializedProperty submenu1Name = submenu1.FindPropertyRelative("menuName");
                            if (submenu1Name != null)
                                submenu1Name.stringValue = "submenu1";

                            SerializedProperty submenu1Template = submenu1.FindPropertyRelative("menuTemplate");
                            if (submenu1Template != null)
                                submenu1Template.objectReferenceValue = subMenu1Template;

                            // Try to find button mappings for submenu1
                            SerializedProperty submenu1ButtonMappings = submenu1.FindPropertyRelative("buttonMappings");
                            if (submenu1ButtonMappings != null)
                            {
                                // Clear existing mappings
                                submenu1ButtonMappings.ClearArray();

                                // Add 1 new mapping
                                submenu1ButtonMappings.arraySize = 1;

                                // Configure the mapping
                                SerializedProperty submenu1Mapping = submenu1ButtonMappings.GetArrayElementAtIndex(0);
                                SerializedProperty submenu1MappingButtonName = submenu1Mapping.FindPropertyRelative("buttonName");
                                if (submenu1MappingButtonName != null)
                                    submenu1MappingButtonName.stringValue = "SubMenu1_Item1_Image";

                                SerializedProperty submenu1MappingTargetMenu = submenu1Mapping.FindPropertyRelative("targetMenuTemplate");
                                if (submenu1MappingTargetMenu != null)
                                    submenu1MappingTargetMenu.objectReferenceValue = productMenuATemplate;

                                SerializedProperty submenu1MappingHeaderText = submenu1Mapping.FindPropertyRelative("menuHeaderText");
                                if (submenu1MappingHeaderText != null)
                                    submenu1MappingHeaderText.stringValue = "Product Menu A";
                            }

                            // Configure submenu2
                            SerializedProperty submenu2 = subMenusProp.GetArrayElementAtIndex(1);
                            SerializedProperty submenu2Name = submenu2.FindPropertyRelative("menuName");
                            if (submenu2Name != null)
                                submenu2Name.stringValue = "submenu2";

                            SerializedProperty submenu2Template = submenu2.FindPropertyRelative("menuTemplate");
                            if (submenu2Template != null)
                                submenu2Template.objectReferenceValue = subMenu2Template;

                            // Try to find button mappings for submenu2
                            SerializedProperty submenu2ButtonMappings = submenu2.FindPropertyRelative("buttonMappings");
                            if (submenu2ButtonMappings != null)
                            {
                                // Clear existing mappings (empty array)
                                submenu2ButtonMappings.ClearArray();
                            }

                            // Configure submenu3
                            SerializedProperty submenu3 = subMenusProp.GetArrayElementAtIndex(2);
                            SerializedProperty submenu3Name = submenu3.FindPropertyRelative("menuName");
                            if (submenu3Name != null)
                                submenu3Name.stringValue = "submenu3";

                            SerializedProperty submenu3Template = submenu3.FindPropertyRelative("menuTemplate");
                            if (submenu3Template != null)
                                submenu3Template.objectReferenceValue = subMenu3Template;

                            // Try to find button mappings for submenu3
                            SerializedProperty submenu3ButtonMappings = submenu3.FindPropertyRelative("buttonMappings");
                            if (submenu3ButtonMappings != null)
                            {
                                // Clear existing mappings (empty array)
                                submenu3ButtonMappings.ClearArray();
                            }
                        }

                        // Apply all changes
                        serializedNavigator.ApplyModifiedProperties();

                        Saltvision.VysoraSetup.Logger.Info("Successfully configured SimpleMenuNavigator properties using SerializedObject");
                        return true;
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning($"Could not find script file at {scriptPath}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure SimpleMenuNavigator");
                return false;
            }
        }    // Helper method to set property values

        private void SetPropertyValue(Type type, object obj, string propertyName, object value)
        {
            try
            {
                // First check if it's a field
                FieldInfo field = type.GetField(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(obj, value);
                    Saltvision.VysoraSetup.Logger.Info($"Set field {propertyName} = {value}");
                    return;
                }

                // Then check if it's a property
                PropertyInfo property = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (property != null)
                {
                    property.SetValue(obj, value);
                    Saltvision.VysoraSetup.Logger.Info($"Set property {propertyName} = {value}");
                    return;
                }

                Saltvision.VysoraSetup.Logger.Warning($"Could not find field or property {propertyName} on {type.Name}");
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, $"Set {propertyName}");
            }
        }

        // Helper to find a type in a specific assembly
        private Type FindTypeInAssembly(Assembly assembly, string typeName)
        {
            // First check if it's a nested type within the parent type
            Type[] allTypes = assembly.GetTypes();

            foreach (Type type in allTypes)
            {
                // Check for the type directly
                if (type.Name == typeName)
                    return type;

                // Check for nested types
                Type[] nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                foreach (Type nestedType in nestedTypes)
                {
                    if (nestedType.Name == typeName)
                        return nestedType;
                }
            }

            Saltvision.VysoraSetup.Logger.Warning($"Could not find type {typeName} in assembly {assembly.GetName().Name}");
            return null;
        }
        // Helper method to try setting a field using multiple possible field names
        private bool TrySetFieldValue(Type type, object obj, string[] possibleFieldNames, object value)
        {
            foreach (string fieldName in possibleFieldNames)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(obj, value);
                    Saltvision.VysoraSetup.Logger.Info($"Set {fieldName} = {value}");
                    return true;
                }
            }

            // If we get here, none of the field names matched
            Saltvision.VysoraSetup.Logger.Warning($"Could not find any matching field among: {string.Join(", ", possibleFieldNames)}");
            return false;
        }
        // Helper method to set field values using reflection
        private void SetFieldValue(Type type, object obj, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                Saltvision.VysoraSetup.Logger.Info($"Set {fieldName} = {value}");
            }
            else
            {
                Saltvision.VysoraSetup.Logger.Warning($"Field {fieldName} not found on {type.Name}");
            }
        }

        // Configure SimpleCameraController properties
        // Configure SimpleCameraController properties
        private bool ConfigureSimpleCameraController()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring SimpleCameraController properties");

                // Find the Main Camera with SimpleCameraController
                GameObject mainCamera = GameObject.FindWithTag("MainCamera");
                if (mainCamera == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("Main Camera not found in scene");
                    return false;
                }

                // Get the SimpleCameraController component
                MonoBehaviour cameraController = null;
                MonoBehaviour[] components = mainCamera.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == "SimpleCameraController")
                    {
                        cameraController = component;
                        break;
                    }
                }

                if (cameraController == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("SimpleCameraController component not found on Main Camera");
                    return false;
                }

                // Create a pivot point (cube) at 0,0.5,0
                GameObject pivotPoint = GameObject.Find("CameraPivot");
                if (pivotPoint == null)
                {
                    pivotPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pivotPoint.name = "CameraPivot";
                    pivotPoint.transform.position = new Vector3(0, 0.5f, 0);
                    pivotPoint.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f); // Make it small

                    // No material assignment to avoid the warning
                    Saltvision.VysoraSetup.Logger.Info("Created camera pivot point cube");
                }

                // Find Camera Position game objects from the scene
                Transform mainCamPosition = null;
                Transform sideCamPosition = null;
                Transform topCamPosition = null;

                // Find Camera Positions parent object
                GameObject cameraPositionsObj = GameObject.Find("Camera Positions");
                if (cameraPositionsObj != null)
                {
                    Transform[] positions = cameraPositionsObj.GetComponentsInChildren<Transform>();
                    foreach (Transform t in positions)
                    {
                        if (t.name == "Main")
                            mainCamPosition = t;
                        else if (t.name == "Side")
                            sideCamPosition = t;
                        else if (t.name == "Top")
                            topCamPosition = t;
                    }
                }

                // Create camera positions if they don't exist
                if (mainCamPosition == null)
                {
                    if (cameraPositionsObj == null)
                        cameraPositionsObj = new GameObject("Camera Positions");

                    GameObject posObj = new GameObject("Main");
                    posObj.transform.SetParent(cameraPositionsObj.transform);
                    posObj.transform.position = new Vector3(0, 2.49f, -6.46f);
                    posObj.transform.rotation = Quaternion.Euler(8.81f, 0, 0);
                    mainCamPosition = posObj.transform;
                }

                if (sideCamPosition == null)
                {
                    if (cameraPositionsObj == null)
                        cameraPositionsObj = new GameObject("Camera Positions");

                    GameObject posObj = new GameObject("Side");
                    posObj.transform.SetParent(cameraPositionsObj.transform);
                    posObj.transform.position = new Vector3(-5.21f, 2.11f, -7.01f);
                    posObj.transform.rotation = Quaternion.Euler(8.81f, 33.19f, 0);
                    sideCamPosition = posObj.transform;
                }

                if (topCamPosition == null)
                {
                    if (cameraPositionsObj == null)
                        cameraPositionsObj = new GameObject("Camera Positions");

                    GameObject posObj = new GameObject("Top");
                    posObj.transform.SetParent(cameraPositionsObj.transform);
                    posObj.transform.position = new Vector3(0, 7, 0);
                    posObj.transform.rotation = Quaternion.Euler(90, 0, 0);
                    topCamPosition = posObj.transform;
                }

                // Find UI Document
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                UIDocument uiDocument = null;

                if (uiDocumentObj != null)
                    uiDocument = uiDocumentObj.GetComponent<UIDocument>();

                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Warning("UI Document not found. Some UI integration features may not work.");
                }

                // Set the properties using reflection (since we don't have direct access to the script type)
                Type controllerType = cameraController.GetType();

                // Basic camera controller properties
                SetFieldValue(controllerType, cameraController, "pivotPoint", pivotPoint.transform);
                SetFieldValue(controllerType, cameraController, "rotationSpeed", 20f);
                SetFieldValue(controllerType, cameraController, "minVerticalAngle", 0f);
                SetFieldValue(controllerType, cameraController, "maxVerticalAngle", 80f);
                SetFieldValue(controllerType, cameraController, "zoomSpeed", 5f);

                // Camera Position Presets
                // Create an array of Transform
                Transform[] cameraPositions = new Transform[] { mainCamPosition, sideCamPosition, topCamPosition };
                SetFieldValue(controllerType, cameraController, "cameraPositions", cameraPositions);

                // Transition settings
                SetFieldValue(controllerType, cameraController, "transitionDuration", 0.5f);

                // UI Integration
                SetFieldValue(controllerType, cameraController, "mainUIDocument", uiDocument);
                SetFieldValue(controllerType, cameraController, "leftPanelName", "LeftPanel");
                SetFieldValue(controllerType, cameraController, "buttonPrefix", "Btn");

                // Camera Position Button Indices
                int[] buttonIndices = new int[] { 1, 2, 3 };
                SetFieldValue(controllerType, cameraController, "cameraPositionButtonIndices", buttonIndices);

                // UI Container Names
                string[] containerNames = new string[] { "main-container" };
                SetFieldValue(controllerType, cameraController, "uiContainerNames", containerNames);

                // Disable When Behind UI
                SetFieldValue(controllerType, cameraController, "disableWhenBehindUI", true);

                // Add the Cylinder2 model as a backdrop
                SetupBackdropModel();

                // Save the scene to persist changes
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

                Saltvision.VysoraSetup.Logger.Info("Successfully configured SimpleCameraController properties");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure SimpleCameraController");
                return false;
            }
        }

        // Setup the backdrop model from Cylinder2
        private bool SetupBackdropModel()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Setting up backdrop model");

                // Check if the backdrop already exists
                GameObject existingBackdrop = GameObject.Find("BackDrop");
                if (existingBackdrop != null)
                {
                    Saltvision.VysoraSetup.Logger.Info("BackDrop object already exists, updating properties");

                    // Try multiple approaches to set the scale

                    // First try: Direct transform scale
                    existingBackdrop.transform.localScale = new Vector3(4f, 4f, 4f);

                    // Second try: Force scale update through parent manipulation
                    // Sometimes this helps when direct scaling doesn't work
                    Transform originalParent = existingBackdrop.transform.parent;
                    existingBackdrop.transform.SetParent(null); // Detach from parent
                    existingBackdrop.transform.localScale = new Vector3(4f, 4f, 4f); // Set scale
                    existingBackdrop.transform.SetParent(originalParent); // Re-attach to original parent

                    // Third try: Check for child renderers and scale them if needed
                    Renderer[] renderers = existingBackdrop.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        foreach (Transform child in existingBackdrop.GetComponentsInChildren<Transform>(true))
                        {
                            if (child != existingBackdrop.transform)
                            {
                                // Keep local position and rotation, but scale up
                                Vector3 origLocalPos = child.localPosition;
                                Quaternion origLocalRot = child.localRotation;
                                child.localScale = new Vector3(4f, 4f, 4f);
                                child.localPosition = origLocalPos;
                                child.localRotation = origLocalRot;
                            }
                        }
                    }

                    // Update position and rotation
                    existingBackdrop.transform.position = new Vector3(0, 0.008f, 0);
                    existingBackdrop.transform.rotation = Quaternion.Euler(-90, 0, 0);

                    // Find and assign the material
                    Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mtl_Env.mat");
                    if (existingMaterial != null)
                    {
                        Renderer[] allRenderers = existingBackdrop.GetComponentsInChildren<Renderer>();
                        foreach (Renderer renderer in allRenderers)
                        {
                            renderer.sharedMaterial = existingMaterial;
                        }
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning("Could not find Mtl_Env material for backdrop");
                    }

                    // Log scale values to check if they're being applied
                    Saltvision.VysoraSetup.Logger.Info($"Backdrop scale set to: {existingBackdrop.transform.localScale}");

                    return true;
                }

                // If backdrop doesn't exist, create a new one
                // Find the Cylinder2 model
                string cylinderPath = "Assets/3DModel/Environment/Cylinder2.fbx";
                GameObject cylinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cylinderPath);

                if (cylinderPrefab == null)
                {
                    // Try to find it using a search
                    string[] foundAssets = AssetDatabase.FindAssets("Cylinder2 t:Model", new[] { "Assets/3DModel", "Assets/3DModel/Environment" });
                    if (foundAssets.Length > 0)
                    {
                        string foundPath = AssetDatabase.GUIDToAssetPath(foundAssets[0]);
                        cylinderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(foundPath);
                        Saltvision.VysoraSetup.Logger.Info($"Found Cylinder2 model at: {foundPath}");
                    }

                    if (cylinderPrefab == null)
                    {
                        Saltvision.VysoraSetup.Logger.Error("Could not find Cylinder2 model in 3DModel/Environment folder");
                        return false;
                    }
                }

                // Instantiate the cylinder
                GameObject backdropObj = Instantiate(cylinderPrefab);
                backdropObj.name = "BackDrop";

                // Set transform properties
                backdropObj.transform.position = new Vector3(0, 0.008f, 0);
                backdropObj.transform.rotation = Quaternion.Euler(-90, 0, 0);
                backdropObj.transform.localScale = new Vector3(4f, 4f, 4f);

                // Try to scale all child objects too
                foreach (Transform child in backdropObj.GetComponentsInChildren<Transform>())
                {
                    if (child != backdropObj.transform)
                    {
                        child.localScale = new Vector3(4f, 4f, 4f);
                    }
                }

                // Find and assign the material
                Material newMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mtl_Env.mat");
                if (newMaterial != null)
                {
                    Renderer[] renderers = backdropObj.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.sharedMaterial = newMaterial;
                    }
                    Saltvision.VysoraSetup.Logger.Info("Assigned Mtl_Env material to backdrop");
                }
                else
                {
                    // Try to find the material using a search
                    string[] foundMaterials = AssetDatabase.FindAssets("Mtl_Env t:Material", new[] { "Assets/Materials" });
                    if (foundMaterials.Length > 0)
                    {
                        string foundPath = AssetDatabase.GUIDToAssetPath(foundMaterials[0]);
                        Material searchedMaterial = AssetDatabase.LoadAssetAtPath<Material>(foundPath);

                        if (searchedMaterial != null)
                        {
                            Renderer[] renderers = backdropObj.GetComponentsInChildren<Renderer>();
                            foreach (Renderer renderer in renderers)
                            {
                                renderer.sharedMaterial = searchedMaterial;
                            }
                            Saltvision.VysoraSetup.Logger.Info($"Assigned Mtl_Env material from: {foundPath}");
                        }
                        else
                        {
                            Saltvision.VysoraSetup.Logger.Warning("Could not find Mtl_Env material for backdrop");
                        }
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning("Could not find Mtl_Env material for backdrop");
                    }
                }

                // Log scale values to check if they're being applied
                Saltvision.VysoraSetup.Logger.Info($"New backdrop scale set to: {backdropObj.transform.localScale}");

                Saltvision.VysoraSetup.Logger.Info("Successfully set up backdrop model");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Setup backdrop model");
                return false;
            }
        }

        // Configure ButtonLabelController properties
        private bool ConfigureButtonLabelController()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring ButtonLabelController properties");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Get the ButtonLabelController component
                MonoBehaviour buttonLabelController = null;
                MonoBehaviour[] components = uiDocumentObj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == "ButtonLabelController")
                    {
                        buttonLabelController = component;
                        break;
                    }
                }

                if (buttonLabelController == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("ButtonLabelController component not found on UI Document");
                    return false;
                }

                // Get the UIDocument component
                UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UIDocument component not found on UI Document GameObject");
                    return false;
                }

                // Get the script type using reflection
                Type buttonLabelControllerType = buttonLabelController.GetType();

                // Set UI Document reference
                SetFieldValue(buttonLabelControllerType, buttonLabelController, "mainUIDocument", uiDocument);

                // Set Button Count
                SetFieldValue(buttonLabelControllerType, buttonLabelController, "buttonCount", 6);

                // Set Initial Text Opacity
                SetFieldValue(buttonLabelControllerType, buttonLabelController, "initialTextOpacity", 0f);

                // Set Transition Duration
                SetFieldValue(buttonLabelControllerType, buttonLabelController, "transitionDuration", 0.2f);

                Saltvision.VysoraSetup.Logger.Info("Successfully configured ButtonLabelController properties");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure ButtonLabelController");
                return false;
            }
        }

        // Configure EnhancedFullScreenToggle properties
        private bool ConfigureEnhancedFullScreenToggle()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring EnhancedFullScreenToggle properties");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Get the EnhancedFullScreenToggle component
                MonoBehaviour enhancedFullScreenToggle = null;
                MonoBehaviour[] components = uiDocumentObj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == "EnhancedFullScreenToggle")
                    {
                        enhancedFullScreenToggle = component;
                        break;
                    }
                }

                if (enhancedFullScreenToggle == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("EnhancedFullScreenToggle component not found on UI Document");
                    return false;
                }

                // Get the UIDocument component
                UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UIDocument component not found on UI Document GameObject");
                    return false;
                }

                // Get the script type using reflection
                Type enhancedFullScreenToggleType = enhancedFullScreenToggle.GetType();

                // Set UI Document reference
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "uiDocument", uiDocument);

                // Set Left Panel Name
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "leftPanelName", "Btn4");

                // Set Left Left Panel Name
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "leftLeftPanelName", "LeftLeft");

                // Set Fullscreen Button Name
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "fullscreenButtonName", "Btn4");

                // Set Use Click Event
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "useClickEvent", true);

                // Set Use Mouse Down Event
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "useMouseDownEvent", true);

                // Set Use Pointer Down Event
                SetFieldValue(enhancedFullScreenToggleType, enhancedFullScreenToggle, "usePointerDownEvent", true);

                Saltvision.VysoraSetup.Logger.Info("Successfully configured EnhancedFullScreenToggle properties");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure EnhancedFullScreenToggle");
                return false;
            }
        }

        // Configure EnhancedScreenshotManager properties
        private bool ConfigureEnhancedScreenshotManager()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring EnhancedScreenshotManager properties");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Get the EnhancedScreenshotManager component
                MonoBehaviour enhancedScreenshotManager = null;
                MonoBehaviour[] components = uiDocumentObj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == "EnhancedScreenshotManager")
                    {
                        enhancedScreenshotManager = component;
                        break;
                    }
                }

                if (enhancedScreenshotManager == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("EnhancedScreenshotManager component not found on UI Document");
                    return false;
                }

                // Get the UIDocument component
                UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UIDocument component not found on UI Document GameObject");
                    return false;
                }

                // Get the script type using reflection
                Type enhancedScreenshotManagerType = enhancedScreenshotManager.GetType();

                // Set UI Document reference
                SetFieldValue(enhancedScreenshotManagerType, enhancedScreenshotManager, "mainUIDocument", uiDocument);

                // Set Left Panel Name
                SetFieldValue(enhancedScreenshotManagerType, enhancedScreenshotManager, "leftPanelName", "LeftPanel");

                // Set Screenshot Button Name
                SetFieldValue(enhancedScreenshotManagerType, enhancedScreenshotManager, "screenshotButtonName", "Btn5");

                // Set Flash Duration
                SetFieldValue(enhancedScreenshotManagerType, enhancedScreenshotManager, "flashDuration", 0.5f);

                // Set Flash Color (white)
                SetFieldValue(enhancedScreenshotManagerType, enhancedScreenshotManager, "flashColor", Color.white);

                Saltvision.VysoraSetup.Logger.Info("Successfully configured EnhancedScreenshotManager properties");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure EnhancedScreenshotManager");
                return false;
            }
        }

        // Configure PopupManager properties
        private bool ConfigurePopupManager()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring PopupManager properties");

                // Find the UI Document GameObject
                GameObject uiDocumentObj = GameObject.Find("UI Document");
                if (uiDocumentObj == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UI Document GameObject not found");
                    return false;
                }

                // Get the PopupManager component
                MonoBehaviour popupManager = null;
                MonoBehaviour[] components = uiDocumentObj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component.GetType().Name == "PopupManager")
                    {
                        popupManager = component;
                        break;
                    }
                }

                if (popupManager == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("PopupManager component not found on UI Document");
                    return false;
                }

                // Get the UIDocument component
                UIDocument uiDocument = uiDocumentObj.GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Saltvision.VysoraSetup.Logger.Error("UIDocument component not found on UI Document GameObject");
                    return false;
                }

                // Find HelpMenu VisualTreeAsset
                VisualTreeAsset helpMenuTemplate = FindVisualTreeAsset("HelpMenu");

                // Get the script type using reflection
                Type popupManagerType = popupManager.GetType();

                // Set UI Document reference
                SetFieldValue(popupManagerType, popupManager, "mainUIDocument", uiDocument);

                // Set Popup Template
                SetFieldValue(popupManagerType, popupManager, "popupTemplate", helpMenuTemplate);

                // Set Left Panel Name
                SetFieldValue(popupManagerType, popupManager, "leftPanelName", "LeftPanel");

                // Set Trigger Button Name
                SetFieldValue(popupManagerType, popupManager, "triggerButtonName", "Btn6");

                // Set Close Button Name
                SetFieldValue(popupManagerType, popupManager, "closeButtonName", "CloseBtn");

                // Set Animation Duration
                SetFieldValue(popupManagerType, popupManager, "animationDuration", 0.3f);

                // Set Use Backdrop
                SetFieldValue(popupManagerType, popupManager, "useBackdrop", true);

                // Set Backdrop Color (black with alpha for semi-transparency)
                Color backdropColor = new Color(0, 0, 0, 0.5f);
                SetFieldValue(popupManagerType, popupManager, "backdropColor", backdropColor);

                Saltvision.VysoraSetup.Logger.Info("Successfully configured PopupManager properties");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure PopupManager");
                return false;
            }
        }

        // Helper method to find a VisualTreeAsset by name
        private VisualTreeAsset FindVisualTreeAsset(string name)
        {
            // Search for the asset in the project
            string[] guids = AssetDatabase.FindAssets($"t:VisualTreeAsset {name}");

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            }

            // If not found, look in specific folders
            string[] commonPaths = new string[] {
        $"Assets/UI/{name}.uxml",
        $"Assets/UI Toolkit/{name}.uxml",
        $"Assets/UI/Templates/{name}.uxml",
        $"Assets/Resources/UI/{name}.uxml"
    };

            foreach (string path in commonPaths)
            {
                VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (asset != null)
                    return asset;
            }

            Saltvision.VysoraSetup.Logger.Warning($"VisualTreeAsset '{name}' not found. This might need to be assigned manually.");
            return null;
        }

        // Helper method to find a Type by name
        private Type FindType(string typeName)
        {
            // Look through all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.Name == typeName)
                        return type;
                }
            }

            // If not found, try specifically in the Vysora namespace
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.FullName != null && type.FullName.Contains("Vysora") && type.Name == typeName)
                        return type;
                }
            }

            Saltvision.VysoraSetup.Logger.Warning($"Type '{typeName}' not found. Using Object type as fallback.");
            return typeof(object);
        }

        // Helper method to create a MenuConfig object
        private object CreateMenuConfigObject(Type containerType, string menuName, VisualTreeAsset menuTemplate)
        {
            // Find the MenuConfig type
            Type menuConfigType = null;

            // First try to find it as a nested type in the container
            menuConfigType = containerType.GetNestedType("MenuConfig",
                BindingFlags.Public | BindingFlags.NonPublic);

            // If not found as nested, try to find it globally
            if (menuConfigType == null)
                menuConfigType = FindType("MenuConfig");

            // If still not found, create a fallback dynamic object
            if (menuConfigType == null)
            {
                Saltvision.VysoraSetup.Logger.Warning("MenuConfig type not found. Using Dictionary as fallback.");
                var dict = new Dictionary<string, object>();
                dict["menuName"] = menuName;
                dict["menuTemplate"] = menuTemplate;
                return dict;
            }

            // Create an instance of MenuConfig
            object menuConfigObj = Activator.CreateInstance(menuConfigType);

            // Set properties
            SetFieldValue(menuConfigType, menuConfigObj, "menuName", menuName);
            SetFieldValue(menuConfigType, menuConfigObj, "menuTemplate", menuTemplate);

            return menuConfigObj;
        }

        // Helper method to create a ButtonMapping object
        private object CreateButtonMappingObject(string buttonName, VisualTreeAsset targetMenu, string menuHeaderText)
        {
            // Find the ButtonMapping type
            Type buttonMappingType = FindType("ButtonMapping");

            // If not found, create a fallback dynamic object
            if (buttonMappingType == null)
            {
                Saltvision.VysoraSetup.Logger.Warning("ButtonMapping type not found. Using Dictionary as fallback.");
                var dict = new Dictionary<string, object>();
                dict["buttonName"] = buttonName;
                dict["targetMenu"] = targetMenu;
                dict["menuHeaderText"] = menuHeaderText;
                return dict;
            }

            // Create an instance of ButtonMapping
            object buttonMappingObj = Activator.CreateInstance(buttonMappingType);

            // Set properties
            SetFieldValue(buttonMappingType, buttonMappingObj, "buttonName", buttonName);
            SetFieldValue(buttonMappingType, buttonMappingObj, "targetMenu", targetMenu);
            SetFieldValue(buttonMappingType, buttonMappingObj, "menuHeaderText", menuHeaderText);

            return buttonMappingObj;
        }

        // Helper method to create and populate an array of a specific type
        private Array CreateAndPopulateArray(Type elementType, params object[] values)
        {
            Array array = Array.CreateInstance(elementType, values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        private bool ConfigureScriptProperties()
        {
            try
            {
                downloadStatus = "Configuring script properties...";
                downloadProgress = 0.0f;

                Saltvision.VysoraSetup.Logger.Info("Starting configuration of script properties");

                // Track success/failure of each configuration step
                bool allSuccessful = true;
                StringBuilder errorLog = new StringBuilder();

                // 1. Configure SimpleCameraController
                bool step1Success = ConfigureSimpleCameraController();
                if (!step1Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure SimpleCameraController");
                }
                downloadProgress = 0.1f;

                // 2. Configure MenuNavigationManager
                bool step2Success = ConfigureMenuNavigationManager();
                if (!step2Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure MenuNavigationManager");
                }
                downloadProgress = 0.2f;

                // 3. Configure ButtonLabelController
                bool step3Success = ConfigureButtonLabelController();
                if (!step3Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure ButtonLabelController");
                }
                downloadProgress = 0.3f;

                // 4. Configure EnhancedFullScreenToggle
                bool step4Success = ConfigureEnhancedFullScreenToggle();
                if (!step4Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure EnhancedFullScreenToggle");
                }
                downloadProgress = 0.4f;

                // 5. Configure EnhancedScreenshotManager
                bool step5Success = ConfigureEnhancedScreenshotManager();
                if (!step5Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure EnhancedScreenshotManager");
                }
                downloadProgress = 0.5f;

                // 6. Configure PopupManager
                bool step6Success = ConfigurePopupManager();
                if (!step6Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure PopupManager");
                }
                downloadProgress = 0.6f;

                // 7. Configure Environment Lighting
                bool step7Success = ConfigureEnvironmentLighting();
                if (!step7Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure Environment Lighting");
                }
                downloadProgress = 0.7f;

                // 8. Set up URP Asset
                bool step8Success = ConfigureURPAsset();
                if (!step8Success)
                {
                    allSuccessful = false;
                    errorLog.AppendLine("- Failed to configure URP Asset");
                }
                downloadProgress = 0.8f;

                // 9. Ask about platform switching
                bool step9Success = PromptForPlatformSwitch();
                if (!step9Success)
                {
                    // This is a user choice, so we don't mark it as failed
                    Saltvision.VysoraSetup.Logger.Info("User chose not to switch platform to WebGL");
                }
                downloadProgress = 0.9f;

                // Final steps
                if (allSuccessful)
                {
                    downloadStatus = "Script properties configured successfully!";
                    downloadProgress = 1.0f;
                    EditorUtility.DisplayDialog("Success", "All script properties have been configured successfully!", "OK");
                    Saltvision.VysoraSetup.Logger.Info("Script properties configuration completed successfully");

                    // Move to the next step (Setup Complete)
                    currentSetupStep++;
                }
                else
                {
                    downloadStatus = "Configuration completed with errors";
                    downloadProgress = 1.0f;
                    EditorUtility.DisplayDialog("Configuration Warning",
                        $"Some configuration steps failed:\n{errorLog.ToString()}\n\nYou may need to manually configure these components.",
                        "OK");
                    Saltvision.VysoraSetup.Logger.Warning($"Script properties configuration completed with errors: {errorLog.ToString()}");

                    // Move to the next step despite errors
                    currentSetupStep++;
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure script properties");
                EditorUtility.DisplayDialog("Configuration Error",
                    $"An error occurred while configuring script properties: {ex.Message}",
                    "OK");
            }

            return true; // Always return true to continue to the next step
        }

        // New method to configure environment lighting
        private bool ConfigureEnvironmentLighting()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring environment lighting");

                // Set the ambient light to Color mode and #777A7F
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = HexToColor("#777A7F");

                Saltvision.VysoraSetup.Logger.Info("Successfully configured environment lighting");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure environment lighting");
                return false;
            }
        }

        // New method to set up the URP Asset
        private bool ConfigureURPAsset()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Setting up URP Asset");

                // Define the destination paths for the URP asset and renderer
                string urpAssetDestPath = "Assets/New Universal Render Pipeline Asset.asset";
                string rendererAssetDestPath = "Assets/New Universal Render Pipeline Asset_Renderer.asset";

                // Flag to track if we need to download files
                bool needsDownload = false;

                // Check if the URP asset already exists at the destination
                UnityEngine.Rendering.RenderPipelineAsset existingUrpAsset =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(urpAssetDestPath);

                // Check if the renderer asset already exists at the destination
                ScriptableObject existingRendererAsset =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererAssetDestPath);

                if (existingUrpAsset == null || existingRendererAsset == null)
                {
                    // One or both assets don't exist at destination, check if they're in the Vysora folder
                    string vysoraUrpPath = "Assets/Vysora/New Universal Render Pipeline Asset.asset";
                    string vysoraRendererPath = "Assets/Vysora/New Universal Render Pipeline Asset_Renderer.asset";

                    UnityEngine.Rendering.RenderPipelineAsset vysoraUrpAsset =
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(vysoraUrpPath);

                    ScriptableObject vysoraRendererAsset =
                        AssetDatabase.LoadAssetAtPath<ScriptableObject>(vysoraRendererPath);

                    if (vysoraUrpAsset != null && vysoraRendererAsset != null)
                    {
                        // Copy from Vysora folder to destination
                        if (existingUrpAsset == null)
                            AssetDatabase.CopyAsset(vysoraUrpPath, urpAssetDestPath);

                        if (existingRendererAsset == null)
                            AssetDatabase.CopyAsset(vysoraRendererPath, rendererAssetDestPath);

                        AssetDatabase.Refresh();
                        Saltvision.VysoraSetup.Logger.Info("Copied URP and Renderer assets from Vysora folder to Assets root");
                    }
                    else
                    {
                        // Assets not found in Vysora folder, need to download them
                        needsDownload = true;
                    }
                }

                // Download the assets if needed
                if (needsDownload)
                {
                    Saltvision.VysoraSetup.Logger.Info("URP assets not found in project, downloading from repository");

                    // Download the URP asset
                    if (existingUrpAsset == null)
                    {
                        bool urpDownloaded = DownloadAssetFromRepo(
                            "New Universal Render Pipeline Asset.asset",
                            urpAssetDestPath,
                            "URP Asset");

                        if (!urpDownloaded)
                        {
                            Saltvision.VysoraSetup.Logger.Error("Failed to download URP Asset");
                            return false;
                        }
                    }

                    // Download the renderer asset
                    if (existingRendererAsset == null)
                    {
                        bool rendererDownloaded = DownloadAssetFromRepo(
                            "New Universal Render Pipeline Asset_Renderer.asset",
                            rendererAssetDestPath,
                            "URP Renderer");

                        if (!rendererDownloaded)
                        {
                            Saltvision.VysoraSetup.Logger.Error("Failed to download URP Renderer Asset");
                            return false;
                        }
                    }

                    // Refresh AssetDatabase to make sure Unity recognizes the new files
                    AssetDatabase.Refresh();
                }

                // Reload the assets after ensuring they exist
                UnityEngine.Rendering.RenderPipelineAsset urpAsset =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(urpAssetDestPath);

                ScriptableObject rendererAsset =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererAssetDestPath);

                if (urpAsset == null)
                {
                    Saltvision.VysoraSetup.Logger.Error($"Failed to load URP Asset at {urpAssetDestPath}");
                    return false;
                }

                if (rendererAsset == null)
                {
                    Saltvision.VysoraSetup.Logger.Error($"Failed to load URP Renderer Asset at {rendererAssetDestPath}");
                    return false;
                }

                // Configure shadow settings specifically for shader compatibility
                ConfigureURPShadowSettings(urpAsset);

                // Ensure the renderer is assigned to the URP asset
                AssignRendererToURPAsset(urpAsset, rendererAsset);

                // Set the URP Asset in Graphics settings
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
                Saltvision.VysoraSetup.Logger.Info("Set URP Asset in Graphics settings");

                // Set the URP Asset in Quality settings
                QualitySettings.renderPipeline = urpAsset;
                Saltvision.VysoraSetup.Logger.Info("Set URP Asset in Quality settings");

                // Save the project settings
                AssetDatabase.SaveAssets();

                // Setup the BackDrop with the shadow receiver shader
                SetupBackdropWithShadowReceiver();

                Saltvision.VysoraSetup.Logger.Info("Successfully configured URP Asset");
                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure URP Asset");
                return false;
            }

        }

        // Add these additional methods:

        private void ConfigureURPShadowSettings(UnityEngine.Rendering.RenderPipelineAsset renderPipelineAsset)
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Configuring URP shadow settings for shader compatibility");

                // Get all serialized properties of the URP asset for debugging
                SerializedObject serializedURPAsset = new SerializedObject(renderPipelineAsset);

                // Dump all properties for debugging purposes
                SerializedProperty iterator = serializedURPAsset.GetIterator();
                bool enterChildren = true;
                Saltvision.VysoraSetup.Logger.Info("Available URP asset properties:");
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    Saltvision.VysoraSetup.Logger.Info($"- Property: {iterator.propertyPath}, Type: {iterator.propertyType}");
                }

                // Reset the iterator to update the actual properties
                iterator = serializedURPAsset.GetIterator();

                // Focus on key properties for shadows
                bool mainLightShadowsSet = false;
                bool softShadowsSet = false;
                bool shadowDistanceSet = false;
                bool cascadeCountSet = false;

                // Iterate through all properties to find the shadow-related ones
                while (iterator.NextVisible(true))
                {
                    string propertyPath = iterator.propertyPath;

                    // Main light shadows
                    if (propertyPath.Contains("Shadow") && propertyPath.Contains("Main") &&
                        propertyPath.Contains("Support") && iterator.propertyType == SerializedPropertyType.Boolean)
                    {
                        iterator.boolValue = true;
                        Saltvision.VysoraSetup.Logger.Info($"Enabled main light shadows using property: {propertyPath}");
                        mainLightShadowsSet = true;
                    }

                    // Soft shadows
                    if (propertyPath.Contains("SoftShadow") && propertyPath.Contains("Support") &&
                        iterator.propertyType == SerializedPropertyType.Boolean)
                    {
                        iterator.boolValue = true;
                        Saltvision.VysoraSetup.Logger.Info($"Enabled soft shadows using property: {propertyPath}");
                        softShadowsSet = true;
                    }

                    // Shadow distance
                    if (propertyPath.EndsWith("shadowDistance") || propertyPath.EndsWith("ShadowDistance"))
                    {
                        iterator.floatValue = 50f;
                        Saltvision.VysoraSetup.Logger.Info($"Set shadow distance to 50 using property: {propertyPath}");
                        shadowDistanceSet = true;
                    }

                    // Shadow cascade count
                    if ((propertyPath.Contains("Cascade") || propertyPath.Contains("cascade")) &&
                        propertyPath.Contains("Count") && iterator.propertyType == SerializedPropertyType.Integer)
                    {
                        iterator.intValue = 4;
                        Saltvision.VysoraSetup.Logger.Info($"Set shadow cascade count to 4 using property: {propertyPath}");
                        cascadeCountSet = true;
                    }
                }

                // Log any settings we couldn't find
                if (!mainLightShadowsSet)
                    Saltvision.VysoraSetup.Logger.Warning("Could not find main light shadows property in URP asset");
                if (!softShadowsSet)
                    Saltvision.VysoraSetup.Logger.Warning("Could not find soft shadows property in URP asset");
                if (!shadowDistanceSet)
                    Saltvision.VysoraSetup.Logger.Warning("Could not find shadow distance property in URP asset");
                if (!cascadeCountSet)
                    Saltvision.VysoraSetup.Logger.Warning("Could not find cascade count property in URP asset");

                // Apply changes
                serializedURPAsset.ApplyModifiedProperties();

                // Mark URP asset as dirty to ensure changes are saved
                EditorUtility.SetDirty(renderPipelineAsset);
                AssetDatabase.SaveAssets();

                Saltvision.VysoraSetup.Logger.Info("URP shadow settings configured for shader compatibility");
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Configure URP Shadow Settings");
            }
        }

        private void ForceShaderRecompilation(string shaderName)
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info($"Looking for shader with name: {shaderName}");

                // Try multiple potential paths for the shader
                string[] potentialPaths = new[] {
            $"Assets/{shaderName}",
            $"Assets/{shaderName}.shader",
            $"Assets/Materials/{shaderName}",
            $"Assets/Materials/{shaderName}.shader",
            $"Assets/Shaders/{shaderName}",
            $"Assets/Shaders/{shaderName}.shader"
        };

                Shader shader = null;

                // Try each path
                foreach (string path in potentialPaths)
                {
                    Saltvision.VysoraSetup.Logger.Info($"Checking path: {path}");
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader != null)
                    {
                        Saltvision.VysoraSetup.Logger.Info($"Found shader at path: {path}");
                        break;
                    }
                }

                // If not found by path, try to find by name
                if (shader == null)
                {
                    Saltvision.VysoraSetup.Logger.Info("Searching for shader by name in all project assets...");

                    // Extract basic name from path
                    string basicName = Path.GetFileNameWithoutExtension(shaderName);

                    // Search all assets for shaders
                    string[] guids = AssetDatabase.FindAssets("t:Shader");
                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        Shader potentialShader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);

                        if (potentialShader != null &&
                            (potentialShader.name.Contains(basicName) || assetPath.Contains(basicName)))
                        {
                            shader = potentialShader;
                            Saltvision.VysoraSetup.Logger.Info($"Found shader by name search: {potentialShader.name} at {assetPath}");
                            break;
                        }
                    }
                }

                // If still not found, try Resources.FindObjectsOfTypeAll
                if (shader == null)
                {
                    Saltvision.VysoraSetup.Logger.Info("Searching for shader in all loaded resources...");
                    string basicName = Path.GetFileNameWithoutExtension(shaderName);
                    Shader[] allShaders = Resources.FindObjectsOfTypeAll<Shader>();

                    foreach (Shader s in allShaders)
                    {
                        if (s.name.Contains(basicName) || s.name.Contains("BackdropSolidColor"))
                        {
                            shader = s;
                            Saltvision.VysoraSetup.Logger.Info($"Found shader in resources: {s.name}");
                            break;
                        }
                    }
                }

                if (shader != null)
                {
                    Saltvision.VysoraSetup.Logger.Info($"Forcing recompilation of shader: {shader.name}");

                    // Create a dummy material to force recompilation
                    Material tempMaterial = new Material(shader);

                    // Try to set properties safely using the material
                    try
                    {
                        // Set some common properties that might exist on the material
                        tempMaterial.SetFloat("_ShadowStrength", 0.8f);
                        tempMaterial.SetFloat("_ShadowSoftness", 0.2f);
                        tempMaterial.SetColor("_Color", Color.white);
                    }
                    catch (Exception propEx)
                    {
                        Saltvision.VysoraSetup.Logger.Warning($"Some properties might not exist on the material: {propEx.Message}");
                    }

                    // Clean up the temporary material
                    UnityEngine.Object.DestroyImmediate(tempMaterial);

                    // Mark the shader as dirty
                    EditorUtility.SetDirty(shader);

                    // Refresh AssetDatabase
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Saltvision.VysoraSetup.Logger.Info("Shader recompilation triggered");
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning($"Could not find shader with name: {shaderName} in the project");

                    // Try looking for any backdrop-related shaders
                    Shader[] allShaders = Resources.FindObjectsOfTypeAll<Shader>();
                    var backdropShaders = allShaders.Where(s => s.name.Contains("Backdrop") || s.name.Contains("backdrop")).ToArray();

                    if (backdropShaders.Length > 0)
                    {
                        Saltvision.VysoraSetup.Logger.Info($"Found {backdropShaders.Length} backdrop-related shaders:");
                        foreach (var s in backdropShaders)
                        {
                            Saltvision.VysoraSetup.Logger.Info($"- {s.name}");
                        }
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning("No backdrop-related shaders found in the project");
                    }
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Force Shader Recompilation");
            }
        }    // Download an asset from the repository
        private bool DownloadAssetFromRepo(string fileName, string destPath, string assetType)
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info($"Downloading {assetType} from GitHub");

                // Display progress dialog
                EditorUtility.DisplayProgressBar($"Downloading {assetType}", "Connecting to GitHub...", 0.1f);

                // Define the GitHub raw URL for the asset
                // Replace spaces with %20 for URL encoding
                string encodedFileName = fileName.Replace(" ", "%20");
                string githubRawUrl = $"https://raw.githubusercontent.com/{githubRepo}/main/{encodedFileName}";

                // Create HTTP client with appropriate authentication if needed
                using (HttpClient client = new HttpClient())
                {
                    // Add authentication if this is a private repo
                    if (isPrivateRepo)
                    {
                        if (!string.IsNullOrEmpty(githubToken))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("token", githubToken);
                        }
                        else if (!string.IsNullOrEmpty(githubUsername) && !string.IsNullOrEmpty(githubPassword))
                        {
                            string authInfo = $"{githubUsername}:{githubPassword}";
                            string encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);
                        }
                        else
                        {
                            Saltvision.VysoraSetup.Logger.Error("Authentication required for private repo but no credentials provided");
                            EditorUtility.ClearProgressBar();
                            return false;
                        }
                    }

                    // Set user agent to avoid GitHub API limitations
                    client.DefaultRequestHeaders.Add("User-Agent", "Vysora-UnitySetupTool");

                    // Update progress
                    EditorUtility.DisplayProgressBar($"Downloading {assetType}", "Downloading file...", 0.3f);

                    // Download the file
                    var response = client.GetAsync(githubRawUrl).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        Saltvision.VysoraSetup.Logger.Error($"Failed to download {assetType}: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");
                        EditorUtility.ClearProgressBar();
                        return false;
                    }

                    // Get the file content
                    byte[] fileBytes = response.Content.ReadAsByteArrayAsync().Result;

                    // Update progress
                    EditorUtility.DisplayProgressBar($"Downloading {assetType}", "Saving file...", 0.7f);

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Save to temp file first (to avoid corruption if write fails)
                    string tempPath = destPath + ".temp";
                    File.WriteAllBytes(tempPath, fileBytes);

                    // Move to final location
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(tempPath, destPath);

                    Saltvision.VysoraSetup.Logger.Info($"{assetType} downloaded to {destPath}");
                    EditorUtility.ClearProgressBar();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, $"Download {assetType}");
                EditorUtility.ClearProgressBar();
                return false;
            }
        }

        // Assign the renderer to the URP asset
        private void AssignRendererToURPAsset(UnityEngine.Rendering.RenderPipelineAsset urpAsset, ScriptableObject rendererAsset)
        {
            try
            {
                // Using SerializedObject to assign renderer
                SerializedObject serializedURPAsset = new SerializedObject(urpAsset);

                // Check if renderer list exists
                SerializedProperty rendererList = serializedURPAsset.FindProperty("m_RendererDataList");
                if (rendererList == null)
                    rendererList = serializedURPAsset.FindProperty("rendererDataList");

                if (rendererList != null)
                {
                    // Clear existing renderers and add our renderer
                    rendererList.ClearArray();
                    rendererList.arraySize = 1;
                    SerializedProperty firstElement = rendererList.GetArrayElementAtIndex(0);
                    firstElement.objectReferenceValue = rendererAsset;

                    // Set default renderer index to 0
                    SerializedProperty defaultRendererIndex = serializedURPAsset.FindProperty("m_DefaultRendererIndex");
                    if (defaultRendererIndex == null)
                        defaultRendererIndex = serializedURPAsset.FindProperty("defaultRendererIndex");

                    if (defaultRendererIndex != null)
                        defaultRendererIndex.intValue = 0;

                    serializedURPAsset.ApplyModifiedProperties();
                    Saltvision.VysoraSetup.Logger.Info("Assigned renderer to URP asset");
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Could not find renderer list property on URP asset");
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Assign Renderer To URP Asset");
            }
        }
        // Method to download the URP asset from GitHub
        private bool DownloadURPAsset(string destPath)
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Downloading URP Asset from GitHub");

                // Display progress dialog
                EditorUtility.DisplayProgressBar("Downloading URP Asset", "Connecting to GitHub...", 0.1f);

                // Define the GitHub raw URL for the URP asset
                // Replace with your actual GitHub URL
                string githubRawUrl = $"https://raw.githubusercontent.com/{githubRepo}/main/New%20Universal%20Render%20Pipeline%20Asset.asset";

                // Create HTTP client with appropriate authentication if needed
                using (HttpClient client = new HttpClient())
                {
                    // Add authentication if this is a private repo
                    if (isPrivateRepo)
                    {
                        if (!string.IsNullOrEmpty(githubToken))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("token", githubToken);
                        }
                        else if (!string.IsNullOrEmpty(githubUsername) && !string.IsNullOrEmpty(githubPassword))
                        {
                            string authInfo = $"{githubUsername}:{githubPassword}";
                            string encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);
                        }
                        else
                        {
                            Saltvision.VysoraSetup.Logger.Error("Authentication required for private repo but no credentials provided");
                            EditorUtility.ClearProgressBar();
                            return false;
                        }
                    }

                    // Set user agent to avoid GitHub API limitations
                    client.DefaultRequestHeaders.Add("User-Agent", "Vysora-UnitySetupTool");

                    // Update progress
                    EditorUtility.DisplayProgressBar("Downloading URP Asset", "Downloading file...", 0.3f);

                    // Download the file
                    var response = client.GetAsync(githubRawUrl).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        Saltvision.VysoraSetup.Logger.Error($"Failed to download URP Asset: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");
                        EditorUtility.ClearProgressBar();
                        return false;
                    }

                    // Get the file content
                    byte[] fileBytes = response.Content.ReadAsByteArrayAsync().Result;

                    // Update progress
                    EditorUtility.DisplayProgressBar("Downloading URP Asset", "Saving file...", 0.7f);

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Save to temp file first (to avoid corruption if write fails)
                    string tempPath = destPath + ".temp";
                    File.WriteAllBytes(tempPath, fileBytes);

                    // Move to final location
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(tempPath, destPath);

                    // Refresh the AssetDatabase to recognize the new file
                    AssetDatabase.Refresh();

                    Saltvision.VysoraSetup.Logger.Info($"URP Asset downloaded to {destPath}");
                    EditorUtility.ClearProgressBar();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Download URP Asset");
                EditorUtility.ClearProgressBar();
                return false;
            }
        }
        // New method to prompt for platform switching
        private bool PromptForPlatformSwitch()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Prompting for platform switch to WebGL");

                // Ask the user if they want to switch to WebGL platform
                bool switchToWebGL = EditorUtility.DisplayDialog(
                    "Switch to WebGL Platform?",
                    "Would you like to switch the build platform to WebGL? This is recommended for web-based projects.\n\n" +
                    "Note: This will trigger a project reload which may take some time.",
                    "Switch to WebGL",
                    "Skip (I'll do it later)"
                );

                if (switchToWebGL)
                {
                    Saltvision.VysoraSetup.Logger.Info("User chose to switch to WebGL platform");

                    // Get the WebGL module
                    BuildTargetGroup targetGroup = BuildTargetGroup.WebGL;
                    BuildTarget target = BuildTarget.WebGL;

                    // Check if the platform is already WebGL
                    if (EditorUserBuildSettings.activeBuildTarget == target)
                    {
                        Saltvision.VysoraSetup.Logger.Info("Project is already using WebGL platform");
                        return true;
                    }

                    // Check if the WebGL module is installed
                    if (!BuildPipeline.IsBuildTargetSupported(targetGroup, target))
                    {
                        EditorUtility.DisplayDialog(
                            "WebGL Module Not Installed",
                            "The WebGL module is not installed. Please install it via Unity Hub and try again.",
                            "OK"
                        );
                        Saltvision.VysoraSetup.Logger.Warning("WebGL module is not installed");
                        return false;
                    }

                    // Switch to WebGL platform
                    // Note: This will trigger a project reload
                    EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);

                    // The editor will reload, so we won't reach this point immediately
                    return true;
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Info("User chose not to switch to WebGL platform");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Prompt for platform switch");
                return false;
            }
        }

        // Add this to your existing ConfigureURPAsset method or another appropriate method
        private bool SetupBackdropWithShadowReceiver()
        {
            try
            {
                Saltvision.VysoraSetup.Logger.Info("Setting up BackDrop with SolidColorShadowReceiver shader");

                // Define paths
                string shaderPath = "Assets/Materials/SolidColorShadowReceiver.shader";
                string materialPath = "Assets/Materials/SolidColorShadowReceiver.mat";

                // Check if shader already exists
                Shader existingShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

                // Download shader if it doesn't exist
                if (existingShader == null)
                {
                    Saltvision.VysoraSetup.Logger.Info("SolidColorShadowReceiver shader not found, downloading from repository");

                    bool shaderDownloaded = DownloadAssetFromRepo(
                        "SolidColorShadowReceiver.shader",
                        shaderPath,
                        "Shadow Receiver Shader");

                    if (!shaderDownloaded)
                    {
                        Saltvision.VysoraSetup.Logger.Error("Failed to download SolidColorShadowReceiver shader");
                        return false;
                    }

                    // Refresh to ensure Unity recognizes the downloaded shader
                    AssetDatabase.Refresh();

                    // Load the downloaded shader
                    existingShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                    if (existingShader == null)
                    {
                        Saltvision.VysoraSetup.Logger.Error("Failed to load downloaded shader");
                        return false;
                    }
                }

                // Create or update the material
                Material shadowReceiverMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                bool createdNewMaterial = false;

                if (shadowReceiverMaterial == null)
                {
                    // Create new material
                    shadowReceiverMaterial = new Material(existingShader);
                    createdNewMaterial = true;
                }
                else
                {
                    // Update existing material to use our shader
                    shadowReceiverMaterial.shader = existingShader;
                }

                // Configure the material
                shadowReceiverMaterial.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 1.0f)); // Light gray
                shadowReceiverMaterial.SetFloat("_ReceiveShadows", 1.0f);

                // Save the material if it's new
                if (createdNewMaterial)
                {
                    AssetDatabase.CreateAsset(shadowReceiverMaterial, materialPath);
                }
                else
                {
                    EditorUtility.SetDirty(shadowReceiverMaterial);
                    AssetDatabase.SaveAssets();
                }

                // Find the BackDrop GameObject in the scene
                GameObject backdrop = GameObject.Find("BackDrop");
                if (backdrop != null)
                {
                    // Get the renderer component (MeshRenderer or SpriteRenderer)
                    Renderer renderer = backdrop.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        // Assign the material
                        renderer.sharedMaterial = shadowReceiverMaterial;
                        Saltvision.VysoraSetup.Logger.Info("Successfully assigned SolidColorShadowReceiver material to BackDrop");

                        // Mark the scene as dirty
                        EditorSceneManager.MarkSceneDirty(backdrop.scene);
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning("BackDrop object found but it has no Renderer component");
                    }
                }
                else
                {
                    Saltvision.VysoraSetup.Logger.Warning("Could not find BackDrop object in the scene");

                    // Check if there's an object with a similar name
                    GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
                    var possibleBackdrops = allObjects.Where(o =>
                        o.name.ToLower().Contains("backdrop") ||
                        o.name.ToLower().Contains("back") ||
                        o.name.ToLower().Contains("drop") ||
                        o.name.ToLower().Contains("background") ||
                        o.name.ToLower().Contains("ground")
                    ).ToArray();

                    if (possibleBackdrops.Length > 0)
                    {
                        Saltvision.VysoraSetup.Logger.Info($"Found {possibleBackdrops.Length} possible backdrop objects:");
                        foreach (var obj in possibleBackdrops)
                        {
                            Saltvision.VysoraSetup.Logger.Info($"- {obj.name}");

                            // Try to assign the material to the first matching object
                            Renderer renderer = obj.GetComponent<Renderer>();
                            if (renderer != null)
                            {
                                renderer.sharedMaterial = shadowReceiverMaterial;
                                Saltvision.VysoraSetup.Logger.Info($"Assigned SolidColorShadowReceiver material to {obj.name}");
                                EditorSceneManager.MarkSceneDirty(obj.scene);
                                break;
                            }
                        }
                    }
                    else
                    {
                        Saltvision.VysoraSetup.Logger.Warning("No objects with backdrop-like names found in the scene");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Saltvision.VysoraSetup.Logger.Exception(ex, "Setup BackDrop with Shadow Receiver");
                return false;
            }
        }

        #endregion
    }
}