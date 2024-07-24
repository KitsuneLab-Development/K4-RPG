#!/bin/bash

# Step 0: Run the compile command
dotnet publish -f net8.0 -c Release

# Step 1: Create directory structure
mkdir -p package/core
mkdir -p package/modules

# Step 2: Copy core compiled files
cp -r src-plugin/bin/K4-RPG/* package/core/

# Step 3: Copy modules compiled files
for dir in modules/*/; do
  subfolder=$(basename "$dir")
  mkdir -p "package/modules/$subfolder"
  cp -r "$dir/src/bin/$subfolder/"* "package/modules/$subfolder/"
done

# Step 4: Remove Mac hidden files
find package -name ".DS_Store" -delete

# Step 5: Create the package
cd package
zip -r K4-RPG-Package.zip core modules

# Package created, return to the original directory
cd ..
echo "Package created: package/K4-RPG-Package.zip"
