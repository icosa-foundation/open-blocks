# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-11-15

### Added
- Initial release of standalone Blocks file format serialization package
- Support for .blocks, .poly, and .peltzer file formats
- `BlocksFileFormat` high-level API for easy file I/O
- `PolySerializer` low-level chunk-based binary serialization
- `MMesh`, `Face`, `Vertex` core data structures
- `PeltzerFile` and `PeltzerFileHandler` for file operations
- Forward and backward compatibility through chunk-based format
- Comprehensive documentation and code examples
- UPM package structure for easy integration
