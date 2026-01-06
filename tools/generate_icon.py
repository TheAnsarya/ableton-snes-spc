#!/usr/bin/env python3
"""
Generate VST3 plugin icon with SNES ABXY buttons and musical note.

Creates PlugIn.ico with multiple sizes.
Renders programmatically with Pillow for gradient support.
Requires: Pillow
Install: pip install Pillow
"""

import math
import sys
from pathlib import Path

try:
	from PIL import Image, ImageDraw, ImageFilter
except ImportError as e:
	print(f"Missing dependency: {e}")
	print("Install with: pip install Pillow")
	sys.exit(1)

# Icon sizes for Windows ICO file
ICO_SIZES = [256, 128, 64, 48, 32, 16]

# PNG export sizes for reference/documentation
PNG_SIZES = [512, 256, 128, 64, 32]

# Note style variants
NOTE_VARIANTS = ['A', 'B', 'C']


def lerp_color(c1: tuple, c2: tuple, t: float) -> tuple:
	"""Linear interpolate between two colors."""
	return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))


def bezier_point(p0, p1, p2, p3, t):
	"""Calculate cubic bezier point."""
	u = 1 - t
	return (
		u*u*u*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t*t*t*p3[0],
		u*u*u*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t*t*t*p3[1]
	)


def draw_smooth_shape(draw, points, fill, outline=None, outline_width=0):
	"""Draw a smooth filled shape with optional outline."""
	if outline and outline_width > 0:
		# Draw outline first by drawing slightly larger
		draw.polygon(points, fill=outline)
	draw.polygon(points, fill=fill)


def draw_gradient_circle(img: Image, cx: int, cy: int, r: int,
						 highlight: tuple, main: tuple, shadow: tuple) -> None:
	"""Draw a circle with radial gradient for 3D button effect."""
	for y in range(cy - r - 2, cy + r + 3):
		for x in range(cx - r - 2, cx + r + 3):
			if x < 0 or y < 0 or x >= img.width or y >= img.height:
				continue
			dx = x - cx
			dy = y - cy
			dist = math.sqrt(dx * dx + dy * dy)

			if dist <= r:
				# Calculate gradient based on distance from highlight point (upper-left)
				hx, hy = cx - r * 0.35, cy - r * 0.35
				hdx = x - hx
				hdy = y - hy
				hdist = math.sqrt(hdx * hdx + hdy * hdy)
				max_hdist = r * 1.7

				t = min(1.0, hdist / max_hdist)

				if t < 0.4:
					color = lerp_color(highlight, main, t / 0.4)
				else:
					color = lerp_color(main, shadow, (t - 0.4) / 0.6)

				# Anti-aliasing at edge
				if dist > r - 1.5:
					alpha = int(255 * max(0, min(1, (r - dist + 1.5) / 1.5)))
					color = color + (alpha,)
				else:
					color = color + (255,)

				img.putpixel((x, y), color)


def draw_sixteenth_note(draw: ImageDraw, img: Image, cx: int, cy: int, scale: float, variant: str = 'A') -> None:
	"""Draw a beautiful sixteenth note with two flowing flags and white outline."""
	# Render at 4x size for anti-aliasing
	aa_scale = 4
	large_size = int(img.width * aa_scale)
	large_img = Image.new('RGBA', (large_size, large_size), (0, 0, 0, 0))
	large_draw = ImageDraw.Draw(large_img)

	s = scale * aa_scale
	lcx = cx * aa_scale
	lcy = cy * aa_scale

	note_color = (20, 20, 20, 255)
	outline_color = (255, 255, 255, 255)
	outline_w = max(8, int(4 * s))

	# Note head parameters - elegant tilted oval
	head_rx = int(24 * s)
	head_ry = int(18 * s)
	head_cx = int(lcx - 18 * s)
	head_cy = int(lcy + 28 * s)
	head_angle = -25  # degrees

	# Stem parameters
	stem_w = max(6, int(6 * s))
	stem_h = int(95 * s)
	stem_x = head_cx + int(head_rx * 0.65)
	stem_top = lcy - int(58 * s)

	# Generate smooth ellipse points
	def ellipse_points(cx, cy, rx, ry, angle_deg, num_points=80):
		angle = math.radians(angle_deg)
		pts = []
		for i in range(num_points):
			t = 2 * math.pi * i / num_points
			x = rx * math.cos(t)
			y = ry * math.sin(t)
			rotx = x * math.cos(angle) - y * math.sin(angle)
			roty = x * math.sin(angle) + y * math.cos(angle)
			pts.append((cx + rotx, cy + roty))
		return pts

	# Draw outline layer
	outline_head = ellipse_points(head_cx, head_cy, head_rx + outline_w, head_ry + outline_w, head_angle)
	large_draw.polygon(outline_head, fill=outline_color)

	# Stem outline
	large_draw.rectangle([
		stem_x - outline_w, stem_top - outline_w,
		stem_x + stem_w + outline_w, head_cy
	], fill=outline_color)

	# Flag outlines - smooth bezier curves
	if variant == 'A':
		_draw_flags_elegant(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=True)
	elif variant == 'B':
		_draw_flags_flowing(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=True)
	else:
		_draw_flags_classic(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=True)

	# Draw fill layer
	fill_head = ellipse_points(head_cx, head_cy, head_rx, head_ry, head_angle)
	large_draw.polygon(fill_head, fill=note_color)

	# Stem fill
	large_draw.rectangle([stem_x, stem_top, stem_x + stem_w, head_cy], fill=note_color)

	# Flag fills
	if variant == 'A':
		_draw_flags_elegant(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=False)
	elif variant == 'B':
		_draw_flags_flowing(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=False)
	else:
		_draw_flags_classic(large_draw, stem_x + stem_w, stem_top, s, outline_w, outline_color, note_color, outline=False)

	# Downsample with high-quality resampling
	small_note = large_img.resize((img.width, img.height), Image.LANCZOS)

	# Composite onto main image
	img.alpha_composite(small_note)


def _draw_flags_elegant(draw, x, y, s, outline_w, outline_color, note_color, outline=False):
	"""Elegant flowing flags with smooth S-curves."""
	flag_len = int(38 * s)
	flag_drop = int(45 * s)
	flag_thick = int(11 * s)
	spacing = int(18 * s)

	for i in range(2):
		fy = y + i * spacing

		# Control points for smooth cubic bezier
		p0 = (x, fy)
		p1 = (x + flag_len * 0.4, fy + flag_drop * 0.1)
		p2 = (x + flag_len * 0.7, fy + flag_drop * 0.6)
		p3 = (x + flag_len, fy + flag_drop)

		# Generate smooth curve points
		top_curve = [bezier_point(p0, p1, p2, p3, t/60) for t in range(61)]

		# Bottom curve (offset for thickness)
		p0b = (x, fy + flag_thick)
		p1b = (x + flag_len * 0.4, fy + flag_drop * 0.1 + flag_thick * 0.8)
		p2b = (x + flag_len * 0.7, fy + flag_drop * 0.6 + flag_thick * 0.5)
		p3b = (x + flag_len * 0.95, fy + flag_drop + flag_thick * 0.2)

		bottom_curve = [bezier_point(p0b, p1b, p2b, p3b, t/60) for t in range(61)]
		bottom_curve.reverse()

		shape = top_curve + bottom_curve

		if outline:
			# Expand shape for outline
			expanded = _expand_polygon(shape, outline_w)
			draw.polygon(expanded, fill=outline_color)
		else:
			draw.polygon(shape, fill=note_color)


def _draw_flags_flowing(draw, x, y, s, outline_w, outline_color, note_color, outline=False):
	"""Flowing ribbon-like flags with gentle waves."""
	flag_len = int(42 * s)
	flag_drop = int(50 * s)
	flag_thick = int(13 * s)
	spacing = int(20 * s)

	for i in range(2):
		fy = y + i * spacing

		# More dramatic S-curve
		p0 = (x, fy)
		p1 = (x + flag_len * 0.25, fy - flag_drop * 0.1)
		p2 = (x + flag_len * 0.6, fy + flag_drop * 0.7)
		p3 = (x + flag_len, fy + flag_drop * 0.85)

		top_curve = [bezier_point(p0, p1, p2, p3, t/60) for t in range(61)]

		p0b = (x, fy + flag_thick)
		p1b = (x + flag_len * 0.25, fy - flag_drop * 0.1 + flag_thick)
		p2b = (x + flag_len * 0.6, fy + flag_drop * 0.7 + flag_thick * 0.6)
		p3b = (x + flag_len * 0.92, fy + flag_drop * 0.85 + flag_thick * 0.15)

		bottom_curve = [bezier_point(p0b, p1b, p2b, p3b, t/60) for t in range(61)]
		bottom_curve.reverse()

		shape = top_curve + bottom_curve

		if outline:
			expanded = _expand_polygon(shape, outline_w)
			draw.polygon(expanded, fill=outline_color)
		else:
			draw.polygon(shape, fill=note_color)


def _draw_flags_classic(draw, x, y, s, outline_w, outline_color, note_color, outline=False):
	"""Classic musical notation style flags."""
	flag_len = int(35 * s)
	flag_drop = int(40 * s)
	flag_thick = int(10 * s)
	spacing = int(16 * s)

	for i in range(2):
		fy = y + i * spacing

		# Traditional flag curve
		p0 = (x, fy)
		p1 = (x + flag_len * 0.5, fy + flag_drop * 0.15)
		p2 = (x + flag_len * 0.85, fy + flag_drop * 0.5)
		p3 = (x + flag_len, fy + flag_drop)

		top_curve = [bezier_point(p0, p1, p2, p3, t/60) for t in range(61)]

		# Tapered bottom
		p0b = (x, fy + flag_thick)
		p1b = (x + flag_len * 0.5, fy + flag_drop * 0.15 + flag_thick * 0.9)
		p2b = (x + flag_len * 0.85, fy + flag_drop * 0.5 + flag_thick * 0.4)
		p3b = (x + flag_len * 0.9, fy + flag_drop + flag_thick * 0.1)

		bottom_curve = [bezier_point(p0b, p1b, p2b, p3b, t/60) for t in range(61)]
		bottom_curve.reverse()

		shape = top_curve + bottom_curve

		if outline:
			expanded = _expand_polygon(shape, outline_w)
			draw.polygon(expanded, fill=outline_color)
		else:
			draw.polygon(shape, fill=note_color)


def _expand_polygon(points, amount):
	"""Expand a polygon outward by a given amount."""
	# Simple expansion by moving each point outward from centroid
	if len(points) < 3:
		return points

	# Calculate centroid
	cx = sum(p[0] for p in points) / len(points)
	cy = sum(p[1] for p in points) / len(points)

	expanded = []
	for px, py in points:
		dx = px - cx
		dy = py - cy
		dist = math.sqrt(dx*dx + dy*dy)
		if dist > 0:
			nx = dx / dist
			ny = dy / dist
			expanded.append((px + nx * amount, py + ny * amount))
		else:
			expanded.append((px, py))

	return expanded


def render_icon(size: int, variant: str = 'A') -> Image:
	"""Render the icon at the specified size with smooth anti-aliasing."""
	img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
	draw = ImageDraw.Draw(img)

	scale = size / 256.0

	# Button positions and colors (Super Famicom)
	buttons = [
		(208, 128, (255, 107, 107), (229, 57, 53), (183, 28, 28)),  # Red - Right
		(128, 208, (255, 245, 157), (253, 216, 53), (249, 168, 37)),  # Yellow - Bottom
		(128, 48, (144, 202, 249), (30, 136, 229), (13, 71, 161)),  # Blue - Top
		(48, 128, (165, 214, 167), (67, 160, 71), (27, 94, 32)),  # Green - Left
	]

	button_r = int(32 * scale)

	# Draw buttons with gradients
	for cx, cy, highlight, main, shadow in buttons:
		scx = int(cx * scale)
		scy = int(cy * scale)
		draw_gradient_circle(img, scx, scy, button_r, highlight, main, shadow)

	# Draw sixteenth note with smooth curves
	draw_sixteenth_note(draw, img, int(128 * scale), int(128 * scale), scale, variant)

	return img


def create_ico(images: list, ico_path: Path) -> None:
	"""Create ICO file from multiple PIL images."""
	images_sorted = sorted(images, key=lambda i: i.width, reverse=True)

	if images_sorted:
		images_sorted[0].save(
			ico_path,
			format='ICO',
			sizes=[(img.width, img.height) for img in images_sorted],
			append_images=images_sorted[1:]
		)
		print(f"  Created: {ico_path.name} with {len(images_sorted)} sizes")


def main():
	script_dir = Path(__file__).parent
	project_root = script_dir.parent
	assets_dir = project_root / "assets" / "icons"

	png_dir = assets_dir / "png"
	ico_dir = assets_dir
	vst3_resource_dir = project_root / "vst3" / "resource"

	png_dir.mkdir(parents=True, exist_ok=True)
	vst3_resource_dir.mkdir(parents=True, exist_ok=True)

	print("SNES SPC VST3 Icon Generator (Smooth Edition)")
	print("=============================================")
	print()
	print("Generating 3 variants with anti-aliased curves:")
	print("  A - Elegant flowing flags")
	print("  B - Flowing ribbon-like flags")
	print("  C - Classic notation style flags")
	print()

	# Generate PNG files for each variant
	for variant in NOTE_VARIANTS:
		variant_dir = png_dir / f"variant-{variant}"
		variant_dir.mkdir(parents=True, exist_ok=True)

		print(f"Generating Variant {variant}...")
		for size in PNG_SIZES:
			png_path = variant_dir / f"snes-spc-icon-{variant}-{size}.png"
			img = render_icon(size, variant)
			img.save(png_path, 'PNG')
			print(f"  {png_path.name} ({size}x{size})")

	print()

	# Generate ICO files for each variant
	for variant in NOTE_VARIANTS:
		print(f"Creating ICO Variant {variant}...")
		ico_images = []
		for size in ICO_SIZES:
			img = render_icon(size, variant)
			ico_images.append(img)

		ico_path = ico_dir / f"PlugIn-{variant}.ico"
		create_ico(ico_images, ico_path)

	print()
	print("Done! Review variants in assets/icons/png/variant-*/")
	print("Rename your preferred variant to PlugIn.ico")


if __name__ == "__main__":
	main()
