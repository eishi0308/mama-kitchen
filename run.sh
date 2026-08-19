#!/bin/bash
# Runs the marketplace app. Open http://localhost:5289 in your browser once it says "Application started".
cd "$(dirname "$0")/Marketplace.Web"
dotnet run --urls "http://localhost:5289"
