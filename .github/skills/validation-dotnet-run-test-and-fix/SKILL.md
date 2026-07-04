---
name: validation-dotnet-run-test-and-fix
description: Run .NET unit tests and fix failing tests to ensure the application passes all test cases. Requires .NET project with test projects configured.
---

# .NET Run Tests and Fix

## Overview

This skill runs .NET unit tests using dotnet test, analyzes test failures, and fixes them to ensure all tests pass.

## User Input

- **project-directory** (Mandatory): The root directory of the .NET project to test
- **migration-folder** (Mandatory): Folder for storing test reports and results
- **test-filter** (Optional): Filter expression to select specific tests to run
- **max-fix-iterations** (Optional): Maximum number of fix attempts (default: 20)

## Workflow

TODO: Implement .NET test run and fix workflow

## Completion Criteria

1. **Tests Run**: All unit tests have been executed
2. **Fixes Applied**: Failing tests have been analyzed and fixed
3. **Report Generated**: Comprehensive test report documents the results and fixes
