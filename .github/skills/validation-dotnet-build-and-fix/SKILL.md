---
name: validation-dotnet-build-and-fix
description: Build .NET projects and fix compilation errors to ensure the application compiles successfully. Requires .NET project with solution or project files, .NET SDK installed and accessible.
---

# .NET Build and Fix

## Overview

This skill builds .NET projects using dotnet CLI, detects compilation errors, and fixes them to ensure the application compiles successfully.

## User Input

- **project-directory** (Mandatory): The root directory of the .NET project to build
- **migration-folder** (Mandatory): Folder for storing build reports and results
- **clean-build** (Optional): Whether to perform a clean build (default: false)
- **max-fix-iterations** (Optional): Maximum number of fix attempts (default: 20)

## Workflow

TODO: Implement .NET build and fix workflow

## Completion Criteria

1. **Build Succeeds**: Project compiles without errors
2. **Fixes Applied**: All compilation errors have been addressed with appropriate fixes
3. **Report Generated**: Comprehensive build report documents the process and results
