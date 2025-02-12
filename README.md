<h1 align="center">File Transformation</h1>
<h2 align="center">A Jellyfin Plugin</h2>
<p align="center">
	<img alt="Logo" src="https://raw.githubusercontent.com/IAmParadox27/jellyfin-plugin-file-transformation/main/src/logo.png" />
	<br />
	<br />
	<a href="https://github.com/IAmParadox27/jellyfin-plugin-home-sections">
		<img alt="GPL 3.0 License" src="https://img.shields.io/github/license/IAmParadox27/jellyfin-plugin-file-transformation.svg" />
	</a>
	<a href="https://github.com/IAmParadox27/jellyfin-plugin-home-sections/releases">
		<img alt="Current Release" src="https://img.shields.io/github/release/IAmParadox27/jellyfin-plugin-file-transformation.svg" />
	</a>
</p>

## Introduction
File Transformation is a Jellyfin Plugin that can be used to modify the served [jellyfin-web](https://github.com/jellyfin/jellyfin-web) content without having to modify the files directly.

The use cases for this can be seen in my other plugins [plugin-pages](https://github.com/IAmParadox27/jellyfin-plugin-pages) and [home-sections](https://github.com/IAmParadox27/jellyfin-plugin-home-sections) which both take advantage of this.

### Credit
The original code and concept for this plugin came from this [Pull Request](https://github.com/jellyfin/jellyfin/pull/9095) from [JPVenson](https://github.com/JPVenson). A lot of the code is unmodified (bar personal code standards).

### Benefits

> Why would I use this rather than just asking installers to change the files?

Well, this plugin is non destructive and allows multiple plugins to manipulate the served data. The install remains clean and free for users to update their Jellyfin server whenever they want.

## Installation

1. Add `https://www.iamparadox.dev/jellyfin/plugins/manifest.json` as a plugin source repository on your Jellyfin server.
2. Find "File Transformation" in the list and install it. No configuration is required.

### Prerequisites
| Plugin Version | Jellyfin Version  |
|----------------|-------------------|
| 2.0.0          | 10.10.5           |

### Referencing this as a library
Due to issues with Jellyfin's plugins being loaded into different load contexts this cannot be referenced directly. 

Instead you can send an HTTP POST request to `http(s)://{YOUR_JELLYFIN_URL}/FileTransformation/RegisterTransformation` with a body in the following format
```json
{
  "id": "00000000-0000-0000-0000-000000000000", // Guid,
  "fileNamePattern": "", // Regex Pattern for the file to patch
  "transformationEndpoint": "/YourPluginController/TransformEndpoint" // An endpoint on your plugin that will accept the content and mutate it.
}
```

When your transformation endpoint is requested you will receive a POST request with the following json format
```json
{
  "contents": "" // String containing the current state of the file being requested.
}
```

## Requests
If any functionality is desired to be overridden from Jellyfin's server please open a `feature-request` issue on GitHub.

## FAQ
> Frequent questions will be added here as they are asked.

Ensure that you check the closed issues on GitHub before asking any questions as they may have already been answered.
