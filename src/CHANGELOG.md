# Changelog

All notable changes to the BHL Unity package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-07-17

Initial release published to OpenUPM.

### Added
- BHL runtime for embedding in Unity — executes compiled `.bhc` bytecode.
- Optional in-editor compiler front-end, enabled via the `BHL_PARSER` / `BHL_FRONT`
  scripting defines when the ANTLR plugin is present.

### Packaging
- Tracked `.meta` files for stable GUIDs across installs.
- Declared `com.unity.nuget.newtonsoft-json` as a dependency.
- Minimum supported Unity version: 2021.3.
