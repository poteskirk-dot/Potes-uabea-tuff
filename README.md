# UABEA Android

An Android-focused UABEA-style Unity asset/bundle editor based on the managed AssetsTools.NET component from the uploaded UABEA-8 source.

## Current Android features
- Open UnityFS bundles and Unity serialized assets files using Android's file picker.
- Browse bundle entries or serialized asset records.
- Search entries.
- Export bundle entries.
- Rename bundle entries.
- Remove bundle entries.
- Import a file into a bundle.
- Save a modified bundle as a new file.
- Android 14 / API 34 SDK build workflow.

## Build
Use GitHub Actions → **Build UABEA Android APK**. The APK is uploaded as an artifact named `UABEA-Android-APK`.

## Important
Texture/audio/font plugin functionality from the desktop UABEA source is not included in this Android-first build because the original project contains desktop/native dependencies. This project focuses on the managed Unity asset/bundle workflow first.
