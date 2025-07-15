# Vysora Project Setup Tool

A Unity Editor plugin to automate project setup and asset management.

## Features

- **Automated Scene Setup**: Quickly create common scene objects with proper hierarchies
- **URP Configuration**: Automatically configure Universal Render Pipeline with optimal settings
- **Asset Management**: Download and install assets from various sources
- **GitHub Integration**: Clone or download specific files from GitHub repositories
- **Project Templates**: Apply predefined templates to quickly start new projects

## Installation

1. In Unity, go to **Window > Package Manager**
2. Click the **+** button and select **Add package from git URL**
3. Enter `https://github.com/vysora/project-setup-tool.git`
4. Click **Add**

Alternatively, download the .unitypackage from the releases page and import it into your project.

## Usage

### Basic Setup

1. Open the tool via **Tools > My Project Setup**
2. Log in with your credentials or GitHub token
3. Click **Complete Setup** to run all setup steps, or use individual buttons for specific tasks

### Asset Management

1. Navigate to the **Assets** tab
2. Add assets by providing a name, URL, and destination path
3. Click **Download** to fetch specific assets
4. Assets can be configured to automatically extract if they're ZIP files

### GitHub Integration

1. Enter GitHub repository details in the **Setup** tab
2. Click **Clone Repository** to download the entire repository or specific folders
3. Use your GitHub Personal Access Token for private repositories

## Configuration

The plugin settings can be found in the **Settings** tab:

- **Server URL**: Configure the base URL for asset downloads
- **Setup Options**: Enable/disable specific setup steps
- **GitHub Settings**: Configure default GitHub repositories

## Requirements

- Unity 2020.3 or later
- Universal Render Pipeline package (installed automatically)

## Support

For issues or feature requests, please visit [our GitHub repository](https://github.com/vysora/project-setup-tool/issues).