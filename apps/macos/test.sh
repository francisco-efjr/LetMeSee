#!/bin/zsh
set -euo pipefail
cd "${0:A:h}"
mkdir -p build
xcrun swiftc -module-cache-path "$PWD/build/module-cache" -swift-version 5 -parse-as-library Sources/Domain.swift Tests/DomainTests.swift -o build/domain-tests
build/domain-tests
xcrun swiftc -module-cache-path "$PWD/build/module-cache" -swift-version 5 -parse-as-library -framework AppKit -framework CryptoKit -framework Network Sources/Domain.swift Sources/Infrastructure.swift Tests/InfrastructureTests.swift -o build/infrastructure-tests
build/infrastructure-tests
