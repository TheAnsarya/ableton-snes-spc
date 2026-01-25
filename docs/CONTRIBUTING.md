# Contributing to SNES SPC VST3 Plugin

**Version**: 0.4.0  
**Last Updated**: January 25, 2026

Thank you for your interest in contributing! This document provides guidelines for contributing to the project.

## Project Status

The plugin is currently at v0.4.0 (Feature Complete) with:
- ✅ Full VSTGUI GUI with custom views
- ✅ SPC import/export with ID666 tags
- ✅ SPCX project format
- ✅ Driver detection (10+ drivers)

Remaining work focuses on polish for v1.0.0.

## Code of Conduct

- Be respectful and inclusive
- Provide constructive feedback
- Focus on what is best for the community
- Show empathy towards other community members

## How to Contribute

### Reporting Bugs

Before creating bug reports, please check existing issues. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce** the issue
- **Expected behavior** vs **actual behavior**
- **Environment details**: OS, DAW version, plugin version
- **SPC file** (if issue is file-specific)
- **Screenshots or audio samples** if applicable

### Suggesting Enhancements

Enhancement suggestions are welcome! Please provide:

- **Clear use case**: What problem does this solve?
- **Detailed description**: How should it work?
- **Examples**: Similar features in other software
- **Implementation ideas** (optional)

### Pull Requests

1. **Fork** the repository
2. **Create a branch** from `main`: `git checkout -b feature/your-feature-name`
3. **Make your changes** following the coding standards
4. **Add tests** for new functionality
5. **Update documentation** as needed
6. **Commit** with clear, descriptive messages
7. **Push** to your fork
8. **Open a Pull Request** against `main`

#### PR Guidelines

- One feature/fix per PR
- Link related issues
- Include tests for new code
- Update docs for user-facing changes
- Follow the existing code style
- Ensure all tests pass
- Keep commits clean and logical

## Development Setup

See [BUILDING.md](BUILDING.md) for build instructions.

### Quick Start for Contributors

```powershell
# Clone your fork
git clone https://github.com/YOUR_USERNAME/ableton-snes-spc.git
cd ableton-snes-spc

# Add upstream remote
git remote add upstream https://github.com/TheAnsarya/ableton-snes-spc.git

# Setup VST3 SDK
.\setup-vst3-sdk.ps1

# Build
.\build-vst3.ps1

# Run tests
cd tests\SpcPlugin.Tests
dotnet test
```

## Coding Standards

### C# (.NET Core)

- **Style**: Follow [.NET coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Naming**:
  - `PascalCase` for classes, methods, properties
  - `camelCase` for local variables, parameters
  - `_camelCase` for private fields
- **Documentation**: XML comments for public APIs
- **Nullability**: Enable nullable reference types
- **Performance**: Use `Span<T>` and `ReadOnlySpan<T>` for buffers
- **Safety**: Avoid unsafe code unless necessary for interop

Example:

```csharp
namespace SpcPlugin.Core.Audio;

/// <summary>
/// Provides BRR audio codec functionality.
/// </summary>
public static class BrrCodec {
	/// <summary>
	/// Decodes a BRR-encoded sample to 16-bit PCM.
	/// </summary>
	/// <param name="brrData">BRR-encoded input data.</param>
	/// <param name="output">Output buffer for PCM samples.</param>
	/// <returns>Number of samples decoded.</returns>
	public static int Decode(ReadOnlySpan<byte> brrData, Span<short> output) {
		// Implementation...
	}
}
```

### C++ (VST3)

- **Style**: Follow [Google C++ Style Guide](https://google.github.io/styleguide/cppguide.html) (with exceptions below)
- **Naming**:
  - `PascalCase` for classes
  - `camelCase` for methods, variables
  - `snake_case` for private members (trailing underscore: `member_`)
- **Modern C++**: Use C++20 features
  - Smart pointers (`std::unique_ptr`, `std::shared_ptr`)
  - `auto` where appropriate
  - Range-based for loops
- **RAII**: Manage resources with constructors/destructors
- **Const-correctness**: Use `const` wherever possible

Example:

```cpp
namespace SnesSpc {

class DotNetHost {
public:
	DotNetHost();
	~DotNetHost();
	
	/// Initialize the .NET runtime host
	bool initialize(const char* libraryPath);
	
	/// Check if initialized
	bool isInitialized() const { return initialized_; }

private:
	bool initialized_ = false;
	void* libraryHandle_ = nullptr;
};

} // namespace SnesSpc
```

### Documentation

- **Code comments**: Explain *why*, not *what*
- **XML docs**: Required for public APIs
- **Markdown**: Use for guides and documentation
- **Examples**: Provide usage examples for complex APIs

## Testing

### Unit Tests

All new features should include unit tests:

```csharp
namespace SpcPlugin.Tests.Audio;

public class BrrCodecTests {
	[Fact]
	public void Decode_ValidBrrBlock_ReturnsCorrectSamples() {
		// Arrange
		byte[] brrData = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];
		var output = new short[16];
		
		// Act
		int samplesDecoded = BrrCodec.Decode(brrData, output);
		
		// Assert
		Assert.Equal(16, samplesDecoded);
		Assert.NotEqual(0, output[0]);
	}
}
```

### Integration Tests

Test real SPC file loading and playback:

```csharp
[Fact]
public void Engine_LoadValidSpc_PlaysAudio() {
	using var engine = new SpcEngine(44100);
	byte[] spcData = LoadTestSpc("test.spc");
	
	engine.LoadSpc(spcData);
	engine.Play();
	
	var buffer = new float[2048];
	engine.Process(buffer, 1024);
	
	Assert.Contains(buffer, s => Math.Abs(s) > 0.001f);
}
```

### Manual Testing

For VST3 plugin:
1. Build in Debug configuration
2. Copy to VST3 folder
3. Load in Ableton Live or another DAW
4. Test with various SPC files
5. Check for memory leaks with diagnostic tools

See [~manual-testing/VST3_TESTING_GUIDE.md](~manual-testing/VST3_TESTING_GUIDE.md)

## Architecture Guidelines

### Separation of Concerns

- **Emulation layer** (`Spc700`, `SDsp`): Pure emulation, no dependencies
- **Audio layer** (`SpcEngine`): Coordinates emulation for audio output
- **Interop layer** (`NativeExports`): C interop boundary
- **VST3 layer**: DAW integration, UI

### Performance Considerations

- **Hot paths**: Audio processing code must be real-time safe
  - No allocations in `Process()` methods
  - No locks in audio thread
  - Pre-allocate buffers
- **Threading**: Separate UI thread from audio thread
- **Emulation accuracy**: Cycle-accurate where needed for audio correctness

### Dependencies

Minimize external dependencies:

- **Core library**: .NET 10 standard library only (+ System.Text.Json)
- **VST3**: Only VST3 SDK and C++ standard library
- **Tests**: xUnit, reasonable testing libraries only

## Documentation Requirements

### For New Features

- **User Guide**: Update with user-facing changes
- **API Reference**: Document all public APIs
- **Examples**: Provide code examples
- **Migration**: Document breaking changes

### For Bug Fixes

- **Changelog**: Add to CHANGELOG.md
- **Tests**: Include regression test
- **Comments**: Explain the fix in code

## Commit Message Format

Use conventional commits:

```
<type>(<scope>): <subject>

<body>

<footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style (formatting, no logic change)
- `refactor`: Code refactor (no feature change)
- `perf`: Performance improvement
- `test`: Adding or updating tests
- `chore`: Build process, dependencies

Examples:

```
feat(audio): add BRR sample encoding support

Implement BRR encoding with automatic filter selection and
loop point optimization. Enables export of edited samples back
to SPC format.

Closes #42
```

```
fix(vst3): resolve crackling on voice key-on

Voice envelope was not properly reset on key-on, causing
interpolation from previous state. Now correctly resets
envelope level to zero.

Fixes #38
```

## Release Process

1. Update version in:
   - `vst3/CMakeLists.txt` (project VERSION)
   - `src/SpcPlugin.Core/SpcPlugin.Core.csproj` (if versioned)
2. Update `CHANGELOG.md`
3. Create tag: `git tag -a v1.0.0 -m "Release 1.0.0"`
4. Push tag: `git push origin v1.0.0`
5. Create GitHub release with binaries

## Getting Help

- **Documentation**: Check [docs/](docs/) folder
- **Issues**: Search existing GitHub issues
- **Discussions**: Start a GitHub Discussion for questions
- **Discord**: (If community server exists)

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (MIT License).

## Recognition

Contributors will be acknowledged in:
- CONTRIBUTORS.md file
- Release notes for significant contributions
- Project README

Thank you for contributing! 🎮🎵
