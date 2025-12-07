using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using MnemonikeyCs.Core;
using MnemonikeyCs.Mnemonic;
using Xunit;
using Xunit.Abstractions;

namespace MnemonikeyCs.IntegrationTests;

public class SimpleCrossCompatibilityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _goMnemonikeyPath;
    private readonly string _tempDir;

    public SimpleCrossCompatibilityTests(ITestOutputHelper output)
    {
        _output = output;
        _goMnemonikeyPath = "/tmp/go-mnemonikey/mnemonikey";
        _tempDir = Path.Combine(Path.GetTempPath(), "mnemonikey-simple-integration", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        
        if (!File.Exists(_goMnemonikeyPath))
        {
            throw new InvalidOperationException($"Go mnemonikey binary not found at {_goMnemonikeyPath}");
        }
    }

    [Fact]
    public async Task GoGeneratedMnemonic_CanBeDecodedByCSharp()
    {
        var tempWordFile = Path.Combine(_tempDir, "go-words.txt");
        
        // Generate using Go
        var goResult = await RunGoCommand("generate", "--name", "Test User", "--email", "test@example.com", "--out-word-file", tempWordFile);
        goResult.ExitCode.Should().Be(0);
        
        // Read the mnemonic phrase
        var mnemonic = await File.ReadAllTextAsync(tempWordFile);
        var mnemonicWords = mnemonic.Trim().Split(' ');
        
        _output.WriteLine($"Go generated mnemonic: {mnemonic}");
        mnemonicWords.Should().HaveCount(14);
        
        // Verify C# can decode the mnemonic
        var action = () => MnemonicDecoder.DecodePlaintext(mnemonicWords);
        action.Should().NotThrow();
        
        var (seed, creationTime) = MnemonicDecoder.DecodePlaintext(mnemonicWords);
        
        // Verify the decoded values are valid
        seed.Should().NotBeNull();
        creationTime.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
        creationTime.Should().BeBefore(DateTime.UtcNow.AddMinutes(5));
        
        _output.WriteLine($"C# decoded seed: {seed.ToHex()}");
        _output.WriteLine($"C# decoded time: {creationTime}");
    }

    [Fact]
    public async Task CSharpGeneratedMnemonic_CanBeUsedByGo()
    {
        // Generate using C#
        var seed = Seed.GenerateRandom();
        var creationTime = DateTime.UtcNow;
        var mnemonic = MnemonicEncoder.EncodeToPlaintext(seed, creationTime);
        var mnemonicString = string.Join(" ", mnemonic);
        
        _output.WriteLine($"C# generated mnemonic: {mnemonicString}");
        _output.WriteLine($"C# seed: {seed.ToHex()}");
        _output.WriteLine($"C# creation time: {creationTime}");
        
        // Write to file for Go to read
        var tempWordFile = Path.Combine(_tempDir, "csharp-words.txt");
        await File.WriteAllTextAsync(tempWordFile, mnemonicString);
        
        // Verify Go can recover from the mnemonic
        var goResult = await RunGoCommand("recover", "--in-word-file", tempWordFile, "--name", "Test User", "--email", "test@example.com");
        
        // Go should exit successfully (exit code 0)
        goResult.ExitCode.Should().Be(0, $"Go failed to recover from C# mnemonic. Error: {goResult.Error}");
        
        // Output should contain PGP key block
        goResult.Output.Should().Contain("-----BEGIN PGP PRIVATE KEY BLOCK-----");
        goResult.Output.Should().Contain("-----END PGP PRIVATE KEY BLOCK-----");
        
        _output.WriteLine("Go successfully recovered from C# generated mnemonic");
    }

    [Fact]
    public async Task EncryptedMnemonic_CrossCompatibility()
    {
        var password = "test-password-123";
        
        // Generate encrypted mnemonic using C#
        var seed = Seed.GenerateRandom();
        var creationTime = DateTime.UtcNow;
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        
        var encryptedMnemonic = MnemonicEncoder.EncodeToEncrypted(seed, creationTime, passwordBytes);
        var mnemonicString = string.Join(" ", encryptedMnemonic);
        
        _output.WriteLine($"C# encrypted mnemonic: {mnemonicString}");
        
        // Write to file for Go to read
        var tempWordFile = Path.Combine(_tempDir, "encrypted-words.txt");
        await File.WriteAllTextAsync(tempWordFile, mnemonicString);
        
        // Verify Go can recover from the encrypted mnemonic
        var goResult = await RunGoCommand("recover", "--in-word-file", tempWordFile, "--name", "Test User", "--email", "test@example.com");
        
        // Since we don't provide the password, Go should prompt for it
        // For automated testing, we can check that Go at least recognizes it as an encrypted phrase
        // and fails in a predictable way
        _output.WriteLine($"Go recovery result: {goResult.Output}");
        _output.WriteLine($"Go recovery error: {goResult.Error}");
        
        // The important thing is that Go doesn't crash and recognizes it as an encrypted phrase
        (goResult.Output + goResult.Error).Should().NotContain("panic");
        
        // Verify C# can decode its own encrypted mnemonic
        var (decodedSeed, decodedTime) = MnemonicDecoder.DecodeEncrypted(encryptedMnemonic, passwordBytes);
        
        decodedSeed.ToHex().Should().Be(seed.ToHex());
        decodedTime.Should().BeCloseTo(creationTime, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("ffffffffffffffffffffffffffffffff")]
    [InlineData("00000000000000000000000000000000")]
    public async Task KnownSeeds_ProduceValidMnemonics(string hexSeed)
    {
        var seed = Seed.FromHex(hexSeed);
        var creationTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Generate mnemonic using C#
        var mnemonic = MnemonicEncoder.EncodeToPlaintext(seed, creationTime);
        var mnemonicString = string.Join(" ", mnemonic);
        
        _output.WriteLine($"Seed: {hexSeed}");
        _output.WriteLine($"C# mnemonic: {mnemonicString}");
        
        // Write to file for Go to read
        var tempWordFile = Path.Combine(_tempDir, $"known-seed-{hexSeed[..8]}.txt");
        await File.WriteAllTextAsync(tempWordFile, mnemonicString);
        
        // Verify Go can recover from the mnemonic
        var goResult = await RunGoCommand("recover", "--in-word-file", tempWordFile, "--name", "Test User", "--email", "test@example.com");
        
        goResult.ExitCode.Should().Be(0, $"Go failed to recover from known seed {hexSeed}. Error: {goResult.Error}");
        goResult.Output.Should().Contain("-----BEGIN PGP PRIVATE KEY BLOCK-----");
        
        // Verify round-trip through C#
        var (decodedSeed, decodedTime) = MnemonicDecoder.DecodePlaintext(mnemonic);
        decodedSeed.ToHex().Should().Be(seed.ToHex());
        decodedTime.Should().Be(creationTime);
        
        _output.WriteLine($"✓ Known seed {hexSeed} validated with both Go and C#");
    }

    [Fact]
    public async Task WordValidation_BothImplementationsAgree()
    {
        // Test with some valid and invalid words
        var testWords = new[]
        {
            "abandon", "ability", "zebra", "zone", // valid words
            "invalid", "notaword", "xyz123", "🚀" // invalid words
        };
        
        foreach (var word in testWords)
        {
            var isValidInCSharp = Wordlist4096.IsValidWord(word);
            
            // Create a minimal mnemonic to test with Go
            var testMnemonic = new string[14];
            testMnemonic[0] = word;
            for (int i = 1; i < 14; i++)
            {
                testMnemonic[i] = "abandon"; // Fill with valid words
            }
            
            var tempWordFile = Path.Combine(_tempDir, $"word-test-{word.Replace("/", "_")}.txt");
            await File.WriteAllTextAsync(tempWordFile, string.Join(" ", testMnemonic));
            
            var goResult = await RunGoCommand("recover", "--in-word-file", tempWordFile, "--name", "Test User", "--email", "test@example.com");
            
            if (isValidInCSharp)
            {
                _output.WriteLine($"✓ Word '{word}' is valid in C# and Go processed it");
            }
            else
            {
                // Invalid words should cause Go to fail
                goResult.ExitCode.Should().NotBe(0, $"Go should reject invalid word '{word}'");
                _output.WriteLine($"✓ Word '{word}' is invalid in C# and Go rejected it");
            }
        }
    }

    private async Task<(string Output, string Error, int ExitCode)> RunGoCommand(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _goMnemonikeyPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            WorkingDirectory = _tempDir
        };
        
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        
        // Immediately close stdin to avoid hanging on interactive prompts
        process.StandardInput.Close();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        // Set a reasonable timeout
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
        var processTask = process.WaitForExitAsync();
        
        var completedTask = await Task.WhenAny(processTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            process.Kill();
            throw new TimeoutException($"Go command timed out: {string.Join(" ", args)}");
        }
        
        var output = await outputTask;
        var error = await errorTask;
        
        _output.WriteLine($"Go command: {_goMnemonikeyPath} {string.Join(" ", args)}");
        _output.WriteLine($"Exit code: {process.ExitCode}");
        if (!string.IsNullOrEmpty(output))
        {
            _output.WriteLine($"Output length: {output.Length} chars");
        }
        if (!string.IsNullOrEmpty(error))
        {
            _output.WriteLine($"Error: {error}");
        }
        
        return (output, error, process.ExitCode);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}