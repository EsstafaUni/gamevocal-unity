# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-08-13
### Changed
- Minor bug fixes and stability improvements.

## [1.0.0] - 2026-08-04
### Added
- Complete rewrite for feature parity with the Godot plugin.
- `GameVocalManager` for runtime dialogue graph traversal and variable state.
- `GameVocalCharacter` as a MonoBehaviour that drives audio and lip-sync.
- `GameVocalLipsyncPlayer` to support ARKit52 blendshape and 2D viseme animation.
- `GameVocalBlendshapeMapperWindow` for automatic and manual ARKit52 blendshape mapping in the editor.
- `GameVocalLocalizationLoader` for runtime translation integration.
- `GameVocalSyncWindow` with incremental sync, manifest tracking, and live polling support.
- Secure API key storage in `UserSettings/GameVocalSettings.json` to support admin privileges.
