#!/bin/zsh
set -euo pipefail
cd "${0:A:h}"
APP="$PWD/build/LetMeSee.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
ARCH="$(uname -m)"
xcrun swiftc -module-cache-path "$PWD/build/module-cache" -swift-version 5 -O -target "$ARCH-apple-macos13.0" -framework SwiftUI -framework AppKit -framework CryptoKit Sources/*.swift -o "$APP/Contents/MacOS/LetMeSee"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>LetMeSee</string>
<key>CFBundleDisplayName</key><string>LetMeSee</string>
<key>CFBundleIdentifier</key><string>com.franciscoefjr.LetMeSee</string>
<key>CFBundleExecutable</key><string>LetMeSee</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.2.0</string>
<key>CFBundleVersion</key><string>12</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
PLIST
print "Build local sem assinatura de distribuição: $APP"
