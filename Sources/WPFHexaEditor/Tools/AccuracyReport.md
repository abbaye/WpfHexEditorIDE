# Format Definition Accuracy Report

**Generated:** 2026-02-22 23:45:26
**Path:** c:\Users\khens\source\repos\WpfHexEditorControl\Sources\WPFHexaEditor\FormatDefinitions
**Known Specifications:** 33 formats

## Summary

- **Total Files:** 426
- **Validated Files:** 53 (with known specs)
- **Accurate Files:** 8
- **Inaccurate Files:** 45
- **No Spec Available:** 373
- **Total Errors:** 70
- **Total Warnings:** 458
- **Accuracy Rate:** 15.1%

## Inaccurate Formats

The following formats have accuracy issues:

### [ERROR] Wavefront OBJ - 3D\OBJ.json

**Errors:**
- Magic bytes mismatch. Expected one of ['52494646', '57415645'], got '23'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, WAVE_signature, fmt_chunk
- Header coverage incomplete: 1 bytes defined, minimum 44 bytes recommended

**Technical References:**
- Microsoft WAVE Format Specification
- RFC 2361

### [ERROR] 7-Zip Archive - Archives\7Z.json

**Errors:**
- Magic bytes mismatch. Expected one of ['504B0304', '504B0506', '504B0708'], got '377ABCAF271C'
- Low essential block coverage: 28.6% (2/7 blocks)

**Warnings:**
- Missing recommended blocks: flags, compression, crc32, compressed_size, uncompressed_size
- Header coverage incomplete: 12 bytes defined, minimum 30 bytes recommended

**Technical References:**
- PKWARE APPNOTE.TXT
- ISO/IEC 21320-1:2015

### [ERROR] BZIP2 Compressed File - Archives\BZIP2.json

**Errors:**
- Magic bytes mismatch. Expected one of ['504B0304', '504B0506', '504B0708'], got '425A68'
- Low essential block coverage: 14.3% (1/7 blocks)

**Warnings:**
- Missing recommended blocks: version, flags, compression, crc32, compressed_size
- Header coverage incomplete: 4 bytes defined, minimum 30 bytes recommended

**Technical References:**
- PKWARE APPNOTE.TXT
- ISO/IEC 21320-1:2015

### [ERROR] GZIP Compressed File - Archives\GZIP.json

**Errors:**
- Magic bytes mismatch. Expected one of ['504B0304', '504B0506', '504B0708'], got '1F8B'
- Low essential block coverage: 28.6% (2/7 blocks)

**Warnings:**
- Missing recommended blocks: signature, version, crc32, compressed_size, uncompressed_size
- Header coverage incomplete: 10 bytes defined, minimum 30 bytes recommended

**Technical References:**
- PKWARE APPNOTE.TXT
- ISO/IEC 21320-1:2015

### [ERROR] RAR Archive - Archives\RAR.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got '526172211A07'

**Warnings:**
- Moderate essential block coverage: 50.0% (2/4 blocks)
- Missing recommended blocks: header_crc, header_type

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] TAR Archive - Archives\TAR.json

**Errors:**
- Low essential block coverage: 12.5% (1/8 blocks)

**Warnings:**
- Missing recommended blocks: filename, uid, gid, size, mtime
- Header coverage incomplete: 262 bytes defined, minimum 512 bytes recommended

**Technical References:**
- POSIX.1-1988
- GNU tar manual

### [ERROR] FLAC Audio - Audio\FLAC.json

**Errors:**
- Low essential block coverage: 33.3% (1/3 blocks)

**Warnings:**
- Missing recommended blocks: metadata_block_header, streaminfo
- Header coverage incomplete: 8 bytes defined, minimum 42 bytes recommended

**Technical References:**
- FLAC Format Specification

### [ERROR] OGG Audio - Audio\OGG.json

**Errors:**
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: capture_pattern, header_type, granule_position
- Header coverage incomplete: 14 bytes defined, minimum 27 bytes recommended

**Technical References:**
- RFC 3533

### [ERROR] WAV Audio - Audio\WAV.json

**Errors:**
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, WAVE_signature, fmt_chunk
- Header coverage incomplete: 16 bytes defined, minimum 44 bytes recommended

**Technical References:**
- Microsoft WAVE Format Specification
- RFC 2361

### [ERROR] WavPack Audio - Audio\WV.json

**Errors:**
- Magic bytes mismatch. Expected one of ['52494646', '57415645'], got '7776706B'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, WAVE_signature, fmt_chunk
- Header coverage incomplete: 4 bytes defined, minimum 44 bytes recommended

**Technical References:**
- Microsoft WAVE Format Specification
- RFC 2361

### [ERROR] Comic Book RAR - Documents\CBR.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got '526172211A07'

**Warnings:**
- Moderate essential block coverage: 50.0% (2/4 blocks)
- Missing recommended blocks: header_crc, header_type

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] Word Document - Documents\DOCX.json

**Errors:**
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: ZIP_signature, version, flags, compression

**Technical References:**
- ECMA-376
- ISO/IEC 29500

### [ERROR] EPUB eBook - Documents\EPUB.json

**Errors:**
- Low essential block coverage: 0.0% (0/2 blocks)

**Warnings:**
- Missing recommended blocks: ZIP_signature, mimetype_file

**Technical References:**
- EPUB 3.2 Specification
- ISO/IEC TS 30135

### [ERROR] DLL Library - Executables\DLL.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got '4D5A'
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: header_crc, header_type, flags

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] Mach-O Executable - Executables\MACH_O.json

**Errors:**
- Low essential block coverage: 20.0% (1/5 blocks)

**Warnings:**
- Missing recommended blocks: cpu_type, cpu_subtype, filetype, ncmds
- Header coverage incomplete: 16 bytes defined, minimum 28 bytes recommended

**Technical References:**
- Mach-O File Format Reference

### [ERROR] Windows Executable (PE) - Executables\PE_EXE.json

**Errors:**
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: DOS_signature, PE_offset, bytes_on_last_page, pages_in_file

**Technical References:**
- Microsoft PE Format Specification

### [ERROR] OpenType Font - Fonts\OTF.json

**Errors:**
- Low essential block coverage: 33.3% (1/3 blocks)

**Warnings:**
- Missing recommended blocks: num_tables, search_range
- Header coverage incomplete: 8 bytes defined, minimum 12 bytes recommended

**Technical References:**
- OpenType Specification
- ISO/IEC 14496-22

### [ERROR] TrueType Font - Fonts\TTF.json

**Errors:**
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: num_tables, search_range, entry_selector
- Header coverage incomplete: 10 bytes defined, minimum 12 bytes recommended

**Technical References:**
- TrueType Reference Manual
- ISO/IEC 14496-22

### [ERROR] Web Open Font Format 2 - Fonts\WOFF2.json

**Errors:**
- Magic bytes mismatch. Expected one of ['774F4646'], got '774F4632'

**Warnings:**
- Moderate essential block coverage: 75.0% (3/4 blocks)
- Missing recommended blocks: num_tables
- Header coverage incomplete: 12 bytes defined, minimum 44 bytes recommended

**Technical References:**
- WOFF File Format 1.0
- W3C Recommendation

### [ERROR] Atari 2600 ROM - Game\ROM_A26.json

**Errors:**
- Magic bytes mismatch. Expected one of ['7573746172'], got '00'
- Magic offset mismatch. Expected 257, got 0
- Low essential block coverage: 0.0% (0/8 blocks)

**Warnings:**
- Missing recommended blocks: filename, mode, uid, gid, size

**Technical References:**
- POSIX.1-1988
- GNU tar manual

### [ERROR] Atari 7800 ROM - Game\ROM_A78.json

**Errors:**
- Magic bytes mismatch. Expected one of ['7573746172'], got '415441524937383030'
- Magic offset mismatch. Expected 257, got 1
- Low essential block coverage: 0.0% (0/8 blocks)

**Warnings:**
- Missing recommended blocks: filename, mode, uid, gid, size

**Technical References:**
- POSIX.1-1988
- GNU tar manual

### [ERROR] Game Boy ROM - Game\ROM_GB.json

**Errors:**
- Magic bytes mismatch. Expected one of ['CEED6666CC0D000B03730083000C000D0008111F8889000EDCCC6EE6DDDDD999BBBB67636E0EECCCDDDC999FBBB9333E'], got 'CEED6666CC0D000B03730083000C000D0008'
- Low essential block coverage: 16.7% (1/6 blocks)

**Warnings:**
- Missing recommended blocks: nintendo_logo, cgb_flag, cartridge_type, rom_size, ram_size

**Technical References:**
- Game Boy Programming Manual
- Pan Docs

### [ERROR] Game Boy Advance ROM - Game\ROM_GBA.json

**Errors:**
- Magic bytes mismatch. Expected one of ['CEED6666CC0D000B03730083000C000D0008111F8889000EDCCC6EE6DDDDD999BBBB67636E0EECCCDDDC999FBBB9333E'], got '96'
- Magic offset mismatch. Expected 260, got 178
- Low essential block coverage: 16.7% (1/6 blocks)

**Warnings:**
- Missing recommended blocks: nintendo_logo, cgb_flag, cartridge_type, rom_size, ram_size

**Technical References:**
- Game Boy Programming Manual
- Pan Docs

### [ERROR] Nintendo 64 ROM - Game\ROM_N64.json

**Errors:**
- Low essential block coverage: 42.9% (3/7 blocks)

**Warnings:**
- Missing recommended blocks: magic, clock_rate, boot_address, game_title

**Technical References:**
- N64 Programming Manual

### [ERROR] NES ROM - Game\ROM_NES.json

**Errors:**
- Low essential block coverage: 20.0% (1/5 blocks)

**Warnings:**
- Missing recommended blocks: prg_rom_size, chr_rom_size, flags6, flags7

**Technical References:**
- iNES Format Specification
- NES 2.0 Specification

### [ERROR] APNG Animated Image - Images\APNG.json

**Errors:**
- Low essential block coverage: 14.3% (1/7 blocks)

**Warnings:**
- Missing recommended blocks: IHDR_length, IHDR_type, width, height, bit_depth
- Header coverage incomplete: 16 bytes defined, minimum 33 bytes recommended

**Technical References:**
- RFC 2325
- ISO/IEC 15948:2004

### [ERROR] AVIF Image - Images\AVIF.json

**Errors:**
- Magic bytes mismatch. Expected one of ['52494646', '41564920'], got '66747970'
- Magic offset mismatch. Expected 0, got 4
- Low essential block coverage: 0.0% (0/3 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, AVI_signature

**Technical References:**
- Microsoft AVI Format Specification

### [ERROR] JPEG Network Graphics - Images\JNG.json

**Errors:**
- Magic bytes mismatch. Expected one of ['FFD8FF'], got '8B4A4E470D0A1A0A'
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: APP0_marker, JFIF_identifier, version
- Header coverage incomplete: 12 bytes defined, minimum 20 bytes recommended

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] JPEG Image - Images\JPEG.json

**Errors:**
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: signature, APP0_marker, JFIF_identifier

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] JPEG 2000 Image - Images\JPEG2000.json

**Errors:**
- Magic bytes mismatch. Expected one of ['FFD8FF'], got '0000000C6A5020200D0A870A'
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: APP0_marker, JFIF_identifier, version

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] JPEG-LS Lossless - Images\JPEG_LS.json

**Errors:**
- Magic bytes mismatch. Expected one of ['FFD8FF'], got 'FFD8FFF7'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: signature, APP0_marker, JFIF_identifier, version
- Header coverage incomplete: 6 bytes defined, minimum 20 bytes recommended

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] JPEG XL Image - Images\JPEG_XL.json

**Errors:**
- Magic bytes mismatch. Expected one of ['FFD8FF'], got 'FF0A'
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: APP0_marker, JFIF_identifier, version
- Header coverage incomplete: 10 bytes defined, minimum 20 bytes recommended

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] Portable Arbitrary Map - Images\PAM.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got '5037'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: signature, header_crc, header_type, flags

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] PNG Image - Images\PNG.json

**Errors:**
- Low essential block coverage: 42.9% (3/7 blocks)

**Warnings:**
- Missing recommended blocks: IHDR_length, IHDR_type, bit_depth, color_type

**Technical References:**
- RFC 2325
- ISO/IEC 15948:2004

### [ERROR] TIFF Image - Images\TIFF.json

**Errors:**
- Low essential block coverage: 0.0% (0/3 blocks)

**Warnings:**
- Missing recommended blocks: byte_order, magic_number, ifd_offset

**Technical References:**
- TIFF 6.0 Specification
- RFC 3302

### [ERROR] WebP Image - Images\WEBP.json

**Errors:**
- Low essential block coverage: 0.0% (0/3 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, WEBP_signature

**Technical References:**
- WebP Container Specification

### [ERROR] PCAPNG Network Capture - Network\PCAPNG.json

**Errors:**
- Magic bytes mismatch. Expected one of ['89504E470D0A1A0A'], got '0A0D0D0A'
- Low essential block coverage: 0.0% (0/7 blocks)

**Warnings:**
- Missing recommended blocks: signature, IHDR_length, IHDR_type, width, height
- Header coverage incomplete: 12 bytes defined, minimum 33 bytes recommended

**Technical References:**
- RFC 2325
- ISO/IEC 15948:2004

### [ERROR] Static Library Archive - Programming\A.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got '213C617263683E0A'
- Low essential block coverage: 25.0% (1/4 blocks)

**Warnings:**
- Missing recommended blocks: header_crc, header_type, flags

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] macOS Dynamic Library - Programming\DYLIB.json

**Errors:**
- Magic bytes mismatch. Expected one of ['526172211A0700', '526172211A070100'], got 'FEEDFACE'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: signature, header_crc, header_type, flags

**Technical References:**
- RAR 5.0 Archive Format

### [ERROR] AVI Video - Video\AVI.json

**Errors:**
- Low essential block coverage: 0.0% (0/3 blocks)

**Warnings:**
- Missing recommended blocks: RIFF_signature, file_size, AVI_signature

**Technical References:**
- Microsoft AVI Format Specification

### [ERROR] Flash MP4 Video - Video\F4V.json

**Errors:**
- Magic bytes mismatch. Expected one of ['66747970'], got '66747970663476'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: box_size, box_type, major_brand, minor_version

**Technical References:**
- ISO/IEC 14496-12
- ISO/IEC 14496-14

### [ERROR] Motion JPEG - Video\MJPEG.json

**Errors:**
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: signature, APP0_marker, JFIF_identifier, version
- Header coverage incomplete: 3 bytes defined, minimum 20 bytes recommended

**Technical References:**
- ISO/IEC 10918-1
- JFIF Specification

### [ERROR] Matroska Video - Video\MKV.json

**Errors:**
- Low essential block coverage: 0.0% (0/3 blocks)

**Warnings:**
- Missing recommended blocks: EBML_signature, EBML_version, doctype

**Technical References:**
- Matroska Specification

### [ERROR] MP4 Video - Video\MP4.json

**Errors:**
- Magic bytes mismatch. Expected one of ['66747970'], got '667479706D703432'
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: box_size, box_type, major_brand, minor_version

**Technical References:**
- ISO/IEC 14496-12
- ISO/IEC 14496-14

### [ERROR] Ogg Video - Video\OGV.json

**Errors:**
- Low essential block coverage: 0.0% (0/4 blocks)

**Warnings:**
- Missing recommended blocks: capture_pattern, version, header_type, granule_position
- Header coverage incomplete: 4 bytes defined, minimum 27 bytes recommended

**Technical References:**
- RFC 3533

## Validated Formats with Warnings

### [WARNING] ZIP Archive - Archives\ZIP.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 57.1%
- **Header Size Coverage:** ✓ Met

**Warnings:**
- Moderate essential block coverage: 57.1% (4/7 blocks)
- Missing recommended blocks: crc32, compressed_size, uncompressed_size

### [WARNING] MP3 Audio - Audio\MP3.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 66.7%
- **Header Size Coverage:** ✓ Met

**Warnings:**
- Moderate essential block coverage: 66.7% (2/3 blocks)
- Missing recommended blocks: signature

### [WARNING] Comic Book ZIP - Documents\CBZ.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 57.1%
- **Header Size Coverage:** ✗ Incomplete

**Warnings:**
- Moderate essential block coverage: 57.1% (4/7 blocks)
- Missing recommended blocks: crc32, compressed_size, uncompressed_size
- Header coverage incomplete: 10 bytes defined, minimum 30 bytes recommended

### [WARNING] ELF Executable - Executables\ELF.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 60.0%
- **Header Size Coverage:** ✗ Incomplete

**Warnings:**
- Moderate essential block coverage: 60.0% (3/5 blocks)
- Missing recommended blocks: data, OS_ABI
- Header coverage incomplete: 18 bytes defined, minimum 52 bytes recommended

### [WARNING] Web Open Font Format - Fonts\WOFF.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 75.0%
- **Header Size Coverage:** ✗ Incomplete

**Warnings:**
- Moderate essential block coverage: 75.0% (3/4 blocks)
- Missing recommended blocks: num_tables
- Header coverage incomplete: 14 bytes defined, minimum 44 bytes recommended

### [WARNING] BMP Image - Images\BMP.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 50.0%
- **Header Size Coverage:** ✗ Incomplete

**Warnings:**
- Moderate essential block coverage: 50.0% (3/6 blocks)
- Missing recommended blocks: file_size, data_offset, header_size
- Header coverage incomplete: 26 bytes defined, minimum 54 bytes recommended

### [WARNING] GIF Image - Images\GIF.json

- **Magic Bytes:** ✓ Valid
- **Magic Offset:** ✓ Valid
- **Essential Blocks Coverage:** 80.0%
- **Header Size Coverage:** ✓ Met

**Warnings:**
- Missing recommended blocks: flags

## Statistics by Category

### 3D

- Total: 19
- Validated: 1
- Accurate: 0
- No Spec: 18
- Accuracy Rate: 0.0%

### Archives

- Total: 28
- Validated: 6
- Accurate: 1
- No Spec: 22
- Accuracy Rate: 16.7%

### Audio

- Total: 30
- Validated: 5
- Accurate: 1
- No Spec: 25
- Accuracy Rate: 20.0%

### CAD

- Total: 21
- Validated: 0
- Accurate: 0
- No Spec: 21
- Accuracy Rate: 0.0%

### Certificates

- Total: 3
- Validated: 0
- Accurate: 0
- No Spec: 3
- Accuracy Rate: 0.0%

### Crypto

- Total: 6
- Validated: 0
- Accurate: 0
- No Spec: 6
- Accuracy Rate: 0.0%

### Data

- Total: 15
- Validated: 0
- Accurate: 0
- No Spec: 15
- Accuracy Rate: 0.0%

### Database

- Total: 18
- Validated: 0
- Accurate: 0
- No Spec: 18
- Accuracy Rate: 0.0%

### Disk

- Total: 10
- Validated: 0
- Accurate: 0
- No Spec: 10
- Accuracy Rate: 0.0%

### Documents

- Total: 28
- Validated: 5
- Accurate: 2
- No Spec: 23
- Accuracy Rate: 40.0%

### Executables

- Total: 6
- Validated: 4
- Accurate: 1
- No Spec: 2
- Accuracy Rate: 25.0%

### Fonts

- Total: 5
- Validated: 4
- Accurate: 1
- No Spec: 1
- Accuracy Rate: 25.0%

### Game

- Total: 64
- Validated: 6
- Accurate: 0
- No Spec: 58
- Accuracy Rate: 0.0%

### Images

- Total: 47
- Validated: 13
- Accurate: 2
- No Spec: 34
- Accuracy Rate: 15.4%

### Medical

- Total: 12
- Validated: 0
- Accurate: 0
- No Spec: 12
- Accuracy Rate: 0.0%

### Network

- Total: 12
- Validated: 1
- Accurate: 0
- No Spec: 11
- Accuracy Rate: 0.0%

### Programming

- Total: 25
- Validated: 2
- Accurate: 0
- No Spec: 23
- Accuracy Rate: 0.0%

### Science

- Total: 27
- Validated: 0
- Accurate: 0
- No Spec: 27
- Accuracy Rate: 0.0%

### System

- Total: 20
- Validated: 0
- Accurate: 0
- No Spec: 20
- Accuracy Rate: 0.0%

### Video

- Total: 30
- Validated: 6
- Accurate: 0
- No Spec: 24
- Accuracy Rate: 0.0%

## Known Format Specifications

This validator currently has technical specifications for 33 formats:

### 7Z

- **Magic Bytes:** 377ABCAF271C
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 32 bytes
- **References:** 7-Zip Format Specification

### AVI

- **Magic Bytes:** 52494646, 41564920
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 12 bytes
- **References:** Microsoft AVI Format Specification

### BMP

- **Magic Bytes:** 424D
- **Magic Offset:** 0
- **Essential Blocks:** 6
- **Min Header Size:** 54 bytes
- **References:** Microsoft BMP Format Specification

### BZIP2

- **Magic Bytes:** 425A68
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 4 bytes
- **References:** bzip2 Format Specification

### DOCX

- **Magic Bytes:** 504B0304
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 30 bytes
- **References:** ECMA-376, ISO/IEC 29500

### ELF

- **Magic Bytes:** 7F454C46
- **Magic Offset:** 0
- **Essential Blocks:** 5
- **Min Header Size:** 52 bytes
- **References:** ELF Specification, System V ABI

### EPUB

- **Magic Bytes:** 504B0304
- **Magic Offset:** 0
- **Essential Blocks:** 2
- **Min Header Size:** 30 bytes
- **References:** EPUB 3.2 Specification, ISO/IEC TS 30135

### FLAC

- **Magic Bytes:** 664C6143
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 42 bytes
- **References:** FLAC Format Specification

### GIF

- **Magic Bytes:** 474946383761, 474946383961
- **Magic Offset:** 0
- **Essential Blocks:** 5
- **Min Header Size:** 13 bytes
- **References:** GIF89a Specification

### GZIP

- **Magic Bytes:** 1F8B
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 10 bytes
- **References:** RFC 1952

### JPEG

- **Magic Bytes:** FFD8FF
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 20 bytes
- **References:** ISO/IEC 10918-1, JFIF Specification

### MACH_O

- **Magic Bytes:** FEEDFACE, FEEDFACF, CEFAEDFE, CFFAEDFE
- **Magic Offset:** 0
- **Essential Blocks:** 5
- **Min Header Size:** 28 bytes
- **References:** Mach-O File Format Reference

### MKV

- **Magic Bytes:** 1A45DFA3
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 4 bytes
- **References:** Matroska Specification

### MP3

- **Magic Bytes:** 494433, FFFB, FFF3, FFF2
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 10 bytes
- **References:** ISO/IEC 11172-3, ISO/IEC 13818-3, ID3v2 Specification

### MP4

- **Magic Bytes:** 66747970
- **Magic Offset:** 4
- **Essential Blocks:** 4
- **Min Header Size:** 8 bytes
- **References:** ISO/IEC 14496-12, ISO/IEC 14496-14

### OGG

- **Magic Bytes:** 4F676753
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 27 bytes
- **References:** RFC 3533

### OTF

- **Magic Bytes:** 4F54544F
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 12 bytes
- **References:** OpenType Specification, ISO/IEC 14496-22

### PDF

- **Magic Bytes:** 25504446
- **Magic Offset:** 0
- **Essential Blocks:** 2
- **Min Header Size:** 8 bytes
- **References:** ISO 32000-1:2008, PDF Reference 1.7

### PE_EXE

- **Magic Bytes:** 4D5A
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 64 bytes
- **References:** Microsoft PE Format Specification

### PNG

- **Magic Bytes:** 89504E470D0A1A0A
- **Magic Offset:** 0
- **Essential Blocks:** 7
- **Min Header Size:** 33 bytes
- **References:** RFC 2325, ISO/IEC 15948:2004

### RAR

- **Magic Bytes:** 526172211A0700, 526172211A070100
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 7 bytes
- **References:** RAR 5.0 Archive Format

### ROM_GB

- **Magic Bytes:** CEED6666CC0D000B03730083000C000D0008111F8889000EDCCC6EE6DDDDD999BBBB67636E0EECCCDDDC999FBBB9333E
- **Magic Offset:** 260
- **Essential Blocks:** 6
- **Min Header Size:** 80 bytes
- **References:** Game Boy Programming Manual, Pan Docs

### ROM_GBA

- **Magic Bytes:** 96
- **Magic Offset:** 178
- **Essential Blocks:** 5
- **Min Header Size:** 192 bytes
- **References:** GBA Technical Manual, GBATEK Specification

### ROM_N64

- **Magic Bytes:** 80371240, 37804012, 40123780, 12408037
- **Magic Offset:** 0
- **Essential Blocks:** 7
- **Min Header Size:** 64 bytes
- **References:** N64 Programming Manual

### ROM_NES

- **Magic Bytes:** 4E45531A
- **Magic Offset:** 0
- **Essential Blocks:** 5
- **Min Header Size:** 16 bytes
- **References:** iNES Format Specification, NES 2.0 Specification

### TAR

- **Magic Bytes:** 7573746172
- **Magic Offset:** 257
- **Essential Blocks:** 8
- **Min Header Size:** 512 bytes
- **References:** POSIX.1-1988, GNU tar manual

### TIFF

- **Magic Bytes:** 49492A00, 4D4D002A
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 8 bytes
- **References:** TIFF 6.0 Specification, RFC 3302

### TTF

- **Magic Bytes:** 00010000, 74727565
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 12 bytes
- **References:** TrueType Reference Manual, ISO/IEC 14496-22

### WAV

- **Magic Bytes:** 52494646, 57415645
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 44 bytes
- **References:** Microsoft WAVE Format Specification, RFC 2361

### WEBP

- **Magic Bytes:** 52494646, 57454250
- **Magic Offset:** 0
- **Essential Blocks:** 3
- **Min Header Size:** 12 bytes
- **References:** WebP Container Specification

### WOFF

- **Magic Bytes:** 774F4646
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 44 bytes
- **References:** WOFF File Format 1.0, W3C Recommendation

### WOFF2

- **Magic Bytes:** 774F4632
- **Magic Offset:** 0
- **Essential Blocks:** 4
- **Min Header Size:** 48 bytes
- **References:** WOFF File Format 2.0, W3C Recommendation

### ZIP

- **Magic Bytes:** 504B0304, 504B0506, 504B0708
- **Magic Offset:** 0
- **Essential Blocks:** 7
- **Min Header Size:** 30 bytes
- **References:** PKWARE APPNOTE.TXT, ISO/IEC 21320-1:2015

## Next Steps

To improve format accuracy:

1. **Fix Inaccurate Formats:** Review and correct formats with errors
2. **Add Missing Specs:** Expand FORMAT_SPECS database with more formats
3. **Complete Block Coverage:** Add missing essential blocks to definitions
4. **Verify Magic Bytes:** Ensure all signatures match official specifications
5. **Test with Real Files:** Validate against actual file samples

