#!/usr/bin/env python3
"""
Generate VST3 plugin icon from SVG source.

Creates PlugIn.ico with multiple sizes:
- 256x256 (Vista+ high-res)
- 128x128
- 64x64
- 48x48 (XP large)
- 32x32 (standard)
- 16x16 (small)

Requires: svglib, reportlab, Pillow
Install: pip install svglib reportlab Pillow
"""

import io
import sys
from pathlib import Path

try:
	from svglib.svglib import svg2rlg
	from reportlab.graphics import renderPM
	from PIL import Image
except ImportError as e:
	print(f"Missing dependency: {e}")
	print("Install with: pip install svglib reportlab Pillow")
	sys.exit(1)

# Icon sizes for Windows ICO file
ICO_SIZES = [256, 128, 64, 48, 32, 16]

# PNG export sizes for reference/documentation
PNG_SIZES = [512, 256, 128, 64, 32]


def svg_to_png(svg_path: Path, output_path: Path, size: int) -> None:
	"""Convert SVG to PNG at specified size."""
	# Load SVG
	drawing = svg2rlg(str(svg_path))
	if drawing is None:
		raise ValueError(f"Failed to load SVG: {svg_path}")

	# Calculate scale factor
	orig_width = drawing.width
	orig_height = drawing.height
	scale = size / max(orig_width, orig_height)

	# Scale the drawing
	drawing.width = size
	drawing.height = size
	drawing.scale(scale, scale)

	# Render to PNG
	renderPM.drawToFile(drawing, str(output_path), fmt="PNG")
	print(f"  Created: {output_path.name} ({size}x{size})")


def create_ico(png_paths: list[Path], ico_path: Path) -> None:
	"""Create ICO file from multiple PNG sizes."""
	images = []
	for png_path in sorted(png_paths, key=lambda p: int(p.stem.split('-')[-1]), reverse=True):
		img = Image.open(png_path)
		# Ensure RGBA mode for ICO
		if img.mode != 'RGBA':
			img = img.convert('RGBA')
		images.append(img)

	# Save as ICO with all sizes
	if images:
		images[0].save(
			ico_path,
			format='ICO',
			sizes=[(img.width, img.height) for img in images],
			append_images=images[1:]
		)
		print(f"  Created: {ico_path.name} with {len(images)} sizes")


def main():
	# Paths
	script_dir = Path(__file__).parent
	project_root = script_dir.parent
	assets_dir = project_root / "assets" / "icons"
	svg_path = assets_dir / "snes-spc-icon.svg"

	# Output directories
	png_dir = assets_dir / "png"
	ico_dir = assets_dir
	vst3_resource_dir = project_root / "vst3" / "resource"

	# Create directories
	png_dir.mkdir(parents=True, exist_ok=True)
	vst3_resource_dir.mkdir(parents=True, exist_ok=True)

	if not svg_path.exists():
		print(f"ERROR: SVG source not found: {svg_path}")
		sys.exit(1)

	print(f"Source SVG: {svg_path}")
	print()

	# Generate PNG files for reference
	print("Generating PNG reference images...")
	for size in PNG_SIZES:
		png_path = png_dir / f"snes-spc-icon-{size}.png"
		svg_to_png(svg_path, png_path, size)

	print()

	# Generate PNGs for ICO
	print("Generating ICO component images...")
	ico_pngs = []
	temp_dir = png_dir / "ico_temp"
	temp_dir.mkdir(exist_ok=True)

	for size in ICO_SIZES:
		png_path = temp_dir / f"icon-{size}.png"
		svg_to_png(svg_path, png_path, size)
		ico_pngs.append(png_path)

	print()

	# Create ICO file
	print("Creating ICO file...")
	ico_path = ico_dir / "PlugIn.ico"
	create_ico(ico_pngs, ico_path)

	# Copy to VST3 resource directory
	vst3_ico_path = vst3_resource_dir / "PlugIn.ico"
	import shutil
	shutil.copy(ico_path, vst3_ico_path)
	print(f"  Copied to: {vst3_ico_path}")

	print()
	print("Icon generation complete!")
	print()
	print("Generated files:")
	print(f"  SVG source:     {svg_path}")
	print(f"  ICO file:       {ico_path}")
	print(f"  VST3 resource:  {vst3_ico_path}")
	print(f"  PNG references: {png_dir}/")

	# Clean up temp files
	for f in temp_dir.glob("*.png"):
		f.unlink()
	temp_dir.rmdir()


if __name__ == "__main__":
	main()
