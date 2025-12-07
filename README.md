# MnemonikeyCs - C# Implementation

[![CI](https://github.com/your-username/mnemonikey-cs/actions/workflows/ci.yml/badge.svg)](https://github.com/your-username/mnemonikey-cs/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MnemonikeyCs.svg)](https://www.nuget.org/packages/MnemonikeyCs/)

### _Deterministic backup and recovery of PGP keys using human-readable phrases - C# Implementation._

This is a C# port of the original [mnemonikey](https://github.com/kklash/mnemonikey) Go implementation by @kklash.

Save your PGP identity as a list of English words. Use these words to recover lost keys or derive new subkeys.

## Features

- **Secure Key Derivation**: Keys derived from seed and creation time using modern algorithms (Argon2id and HKDF)
- **Version Compatibility**: Recovery phrases include version numbers for forwards-compatibility
- **Human-Readable Backup**: Uses a custom high-density wordlist with stronger guarantees than BIP39
- **Built-in Verification**: Phrases include checksums to confirm correct entry
- **Optional Encryption**: Support for encrypted recovery phrases with user passwords
- **Cross-Platform**: Runs on Windows, macOS, and Linux with .NET 8
- **Fully Auditable**: Clean, well-documented C# codebase
- **High Performance**: Optimized cryptographic operations using modern .NET APIs

## Installation

### CLI Tool

```bash
# Install as global tool
dotnet tool install --global MnemonikeyCs.Cli

# Or download pre-built binaries from releases
# https://github.com/your-username/mnemonikey-cs/releases
```

### Library

```bash
dotnet add package MnemonikeyCs
```

## Quick Start

### Generate a new PGP key with recovery phrase

```bash
mnemonikey-cs generate --name "John Doe" --email "john@example.com"
```

### Recover PGP key from existing phrase

```bash
mnemonikey-cs recover --name "John Doe" --email "john@example.com"
```

### Convert between phrase formats

```bash
# Add password protection to existing phrase
mnemonikey-cs convert --encrypt-phrase

# Remove password protection
mnemonikey-cs convert --decrypt-phrase
```

## Library Usage

```csharp
using MnemonikeyCs;
using MnemonikeyCs.Core;

// Generate new seed and key
var seed = Seed.GenerateRandom();
var options = new KeyOptions
{
    Name = "John Doe",
    Email = "john@example.com",
    TTL = TimeSpan.FromDays(365 * 2) // 2 years
};

var mnemonikey = new Mnemonikey(seed, DateTime.UtcNow, options);

// Get recovery phrase
var recoveryPhrase = mnemonikey.EncodeMnemonicPlaintext();
Console.WriteLine($"Recovery phrase: {string.Join(" ", recoveryPhrase)}");

// Export PGP key
var pgpKey = mnemonikey.EncodePGPArmor();
Console.WriteLine(pgpKey);

// Later: recover from phrase
var recoveredSeed = RecoveryPhrase.DecodePlaintext(recoveryPhrase);
var recoveredMnemonikey = new Mnemonikey(recoveredSeed.Seed, recoveredSeed.CreationTime, options);
```

## Architecture

The C# implementation maintains full compatibility with the original Go version while leveraging .NET's strengths:

### Core Components

1. **Cryptographic Layer**
   - `Argon2id` for key derivation (Konscious.Security.Cryptography)
   - `Ed25519` and `Curve25519` operations (NSec.Cryptography)
   - `HKDF` for key expansion (built-in .NET APIs)

2. **PGP Implementation**
   - OpenPGP packet serialization
   - Key binding and self-certification
   - S2K password encryption support

3. **Mnemonic System**
   - Wordlist4096 compatibility
   - Bit-level encoding/decoding
   - CRC32 checksums

4. **CLI Interface**
   - Modern command-line parsing (System.CommandLine)
   - Rich terminal UI (Spectre.Console)
   - Cross-platform compatibility

## Project Structure

```
MnemonikeyCs/
├── src/
│   ├── MnemonikeyCs/              # Core library
│   │   ├── Core/                  # Core types and interfaces
│   │   ├── Crypto/                # Cryptographic operations
│   │   ├── Pgp/                   # PGP key handling
│   │   ├── Mnemonic/              # Mnemonic encoding/decoding
│   │   └── Extensions/            # Utility extensions
│   └── MnemonikeyCs.Cli/          # Command-line interface
├── tests/
│   └── MnemonikeyCs.Tests/        # Comprehensive test suite
├── docs/                          # Documentation
└── scripts/                       # Build and deployment scripts
```

## Compatibility

This implementation maintains 100% compatibility with the original mnemonikey:

- Same recovery phrase format
- Same PGP key derivation
- Same cryptographic parameters
- Cross-interoperable with Go version

## Testing

The project includes comprehensive test coverage:

- Unit tests for all core components
- Integration tests with the original Go implementation
- Cross-platform compatibility tests
- Performance benchmarks
- Security test vectors

```bash
dotnet test
```

## Security

This implementation follows the same security practices as the original:

- Reproducible builds
- Memory-safe operations
- Secure random number generation
- Constant-time cryptographic operations where applicable
- Comprehensive input validation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This project is licensed under the same terms as the original mnemonikey project.

## Acknowledgments
- Original mnemonikey implementation by [@kklash](https://github.com/kklash)
- Wordlist4096 project
- .NET cryptographic libraries maintainers
- Migration from go to C# done by [@MarkBlah](https://github.com/MarkBlah)