#!/bin/bash
# Runs the marketplace app. Open http://localhost:5238 in your browser once it says "Application started".
# The port comes from applicationUrl in Marketplace.Web/Properties/launchSettings.json — don't repeat it here.
cd "$(dirname "$0")/Marketplace.Web"
dotnet run
