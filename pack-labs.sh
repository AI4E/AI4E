#!/bin/bash
dotnet tool restore
dotnet pwsh ./pack-labs.ps1 $@