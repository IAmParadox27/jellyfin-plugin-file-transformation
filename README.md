<h1 align="center">File Transformation</h1>
<h2 align="center">A Jellyfin Plugin</h2>
<p align="center">
	<img alt="Logo" width="256" height="256" src="https://camo.githubusercontent.com/ab4b1ec289bed0a0ac8dd2828c41b695dbfeaad8c82596339f09ce23b30d3eb3/68747470733a2f2f63646e2e6a7364656c6976722e6e65742f67682f73656c666873742f69636f6e732f776562702f6a656c6c7966696e2e77656270" />
	<br />
	<sub>Custom Logo Coming Soon</sub>
	<br />
	<br />
	<a href="https://github.com/IAmParadox27/jellyfin-plugin-home-sections">
		<img alt="GPL 3.0 License" src="https://img.shields.io/github/license/IAmParadox27/jellyfin-plugin-file-transformation.svg" />
	</a>
	<a href="https://github.com/IAmParadox27/jellyfin-plugin-home-sections/releases">
		<img alt="Current Release" src="https://img.shields.io/github/release/IAmParadox27/jellyfin-plugin-file-transformation.svg" />
	</a>
	<a href="https://www.nuget.org/packages/Jellyfin.Plugin.FileTransformation">
		<img alt="NuGet Release" src="https://img.shields.io/nuget/v/Jellyfin.Plugin.FileTransformation" />
	</a>
  <a href="https://www.nuget.org/packages/Jellyfin.Plugin.Referenceable/1.2.1">
    <img alt="Shield Example" src="https://img.shields.io/badge/JF%20Referenceable-v1.2.1-blue" /> 
  </a>
</p>

## Introduction
File Transformation is a [Referenceable](https://github.com/IAmParadox27/jellyfin-plugin-referenceable) Jellyfin Plugin (and C# Library) that can be used to modify the served [jellyfin-web](https://github.com/jellyfin/jellyfin-web) content without having to modify the files directly.

> :warning: This plugin makes use of [Jellyfin Referenceable](https://github.com/IAmParadox27/jellyfin-plugin-referenceable) using version `1.2.1`. Ensure all plugins that use this plugin are compatible with that version.

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
| Plugin Version | Jellyfin Version |
|----------------|------------------|
| 1.1.0          | 10.10.3          |
| 1.2.*          | 10.10.5          |

### Referencing this as a library
Add `Jellyfin.Plugin.FileTransformation` from NuGet using the most appropriate version from the table above. All versions previous to 1.1.0 do not work correctly and have issues which were only discovered after making the release.

```xml
<PackageReference Include="Jellyfin.Plugin.FileTransformation" Version="1.2.1" />
```

## Requests
If any functionality is desired to be overridden from Jellyfin's server please open a `feature-request` issue on GitHub.

## FAQ
> Frequent questions will be added here as they are asked.

Ensure that you check the closed issues on GitHub before asking any questions as they may have already been answered.
