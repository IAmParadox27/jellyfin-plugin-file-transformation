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
	<a href='https://ko-fi.com/iamparadox27' target='_blank'><img height='20' style='border:0px;height:20px;' src='https://storage.ko-fi.com/cdn/kofi4.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
</p>

## Development Update - 20th August 2025

Hey all! Things are changing with my plugins are more and more people start to use them and report issues. In order to make it easier for me to manage I'm splitting bugs and features into different areas. For feature requests please head over to <a href="https://features.iamparadox.dev/">https://features.iamparadox.dev/</a> where you'll be able to signin with GitHub and make a feature request. For bugs please report them on the relevant GitHub repo and they will be added to the <a href="https://github.com/users/IAmParadox27/projects/1/views/1">project board</a> when I've seen them. I've found myself struggling to know when issues are made and such recently so I'm also planning to create a system that will monitor a particular view for new issues that come up and send me a notification which should hopefully allow me to keep more up to date and act faster on various issues.

As with a lot of devs, I am very momentum based in my personal life coding and there are often times when these projects may appear dormant, I assure you now that I don't plan to let these projects go stale for a long time, there just might be times where there isn't an update or response for a couple weeks, but I'll try to keep that better than it has been. With all new releases to Jellyfin I will be updating as soon as possible, I have already made a start on 10.11.0 and will release an update to my plugins hopefully not long after that version is officially released!

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

### FAQ

#### I've updated Jellyfin to latest version but I can't see the plugin available in the catalogue

The likelihood is the plugin hasn't been updated for that version of Jellyfin and the plugins are strictly 1 version compatible. Please wait until an update has been pushed. If you can see the version number in the release assets then please make an issue, but if its not in the assets, please wait. I know Jellyfin has updated, I'll update when I can.

### Referencing this as a library
Due to issues with Jellyfin's plugins being loaded into different load contexts this cannot be referenced directly.

Instead you can use reflection to invoke the plugin directly to register your transformation.

1. Prepare your payload
```json
{
    "id": "00000000-0000-0000-0000-000000000000", // Guid
	"fileNamePattern": "", // Regex Patterm for the file to patch
	"callbackAssembly": GetType().Assembly.FullName, // Example value is a string from C# that should be resolved before adding to json
	"callbackClass": "", // The name of the class that should be invoked from the above assembly
	"callbackMethod": "" // The name of the function that should be invoked from the above class
}
```
2. Send your payload to the file transformation assembly
```csharp

Assembly? fileTransformationAssembly =
	AssemblyLoadContext.All.SelectMany(x => x.Assemblies).FirstOrDefault(x =>
		x.FullName?.Contains(".FileTransformation") ?? false);

if (fileTransformationAssembly != null)
{
	Type? pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");

	if (pluginInterfaceType != null)
	{
		pluginInterfaceType.GetMethod("RegisterTransformation")?.Invoke(null, new object?[] { payload });
	}
}
```

When your transformation method is invoked you will receive a object representing the following json format
```json
{
  "contents": "" // String containing the current state of the file being requested.
}
```

## Contribution

### Code Contributions

You're more than welcome to contribute to this plugin in any way that betters it! I only ask that you follow the same code style as myself. A few points to note:

- Please don't commit with any whitespace changes, might be worth turning off auto-linters
- Please don't use `var` unless you have to due to differing namespaces between JF versions (honestly, I'm not going to gripe for the odd one, but it's good to try at least)
- Please at least check the plugin compiles with 10.10.7 and the latest version of JF
- Please put braces on new lines and use them even for 1 line statements

As a general rule of thumb, please try to blend in with the codebase, I use a mutated hungarian notation for my coding style, I will ask for this to be followed.

After following these guidelines, please create a pull request and I'll review it as soon as I can.

Please declare your AI usage, if any, and if you have used AI for your change, please also declare your coding experience without AI. This won't impact whether the change is merged, it will allow me to properly and correctly review the change to ensure that nothing slips through the net.

## Requests
If any functionality is desired to be overridden from Jellyfin's server please open a request on https://features.iamparadox.dev.

## FAQ
> Frequent questions will be added here as they are asked.

Ensure that you check the closed issues on GitHub before asking any questions as they may have already been answered.
