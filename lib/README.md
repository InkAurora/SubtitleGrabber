# Jellyfin Assembly References

This directory should contain the Jellyfin assemblies required for building the plugin.

## Required Files

You need to copy the following files from your Jellyfin installation:

- `MediaBrowser.Common.dll`
- `MediaBrowser.Controller.dll`
- `MediaBrowser.Model.dll`

## Where to Find These Files

### Windows

Copy from your Jellyfin installation directory:

```
C:\Program Files\Jellyfin\Server\
```

### Linux

Copy from:

```
/usr/lib/jellyfin/bin/
```

### Docker

Extract from the Jellyfin Docker image or copy from a running container:

```bash
docker cp jellyfin-container:/jellyfin/MediaBrowser.Common.dll ./lib/
docker cp jellyfin-container:/jellyfin/MediaBrowser.Controller.dll ./lib/
docker cp jellyfin-container:/jellyfin/MediaBrowser.Model.dll ./lib/
```

## Alternative: Download from GitHub

You can also download these assemblies from the Jellyfin releases on GitHub:

1. Go to https://github.com/jellyfin/jellyfin/releases
2. Download the latest server release
3. Extract and copy the required DLL files to this lib/ directory

## Build Without References

If you don't have access to these assemblies, you can modify the project file to use interface-only compilation, but this will require additional work to define the interfaces manually.
