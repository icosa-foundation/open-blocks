#!/usr/bin/env python3
"""
# UVX Metadata
dependencies:
  - Pillow==10.2.0
description: Inverts all PNG images in the current directory overwriting the original files
version: 1.0.0
author: Assistant
"""

from PIL import Image
import os

def invert_image(image_path):
    # Open the image
    with Image.open(image_path) as img:
        # Store original mode
        original_mode = img.mode
        
        # Split into bands and invert each one
        if original_mode == 'RGBA':
            # Handle RGBA images - preserve alpha channel
            r, g, b, a = img.split()
            r = Image.eval(r, lambda x: 255 - x)
            g = Image.eval(g, lambda x: 255 - x)
            b = Image.eval(b, lambda x: 255 - x)
            inverted_img = Image.merge('RGBA', (r, g, b, a))
        else:
            # Handle all other modes
            bands = img.split()
            inverted_bands = [Image.eval(band, lambda x: 255 - x) for band in bands]
            inverted_img = Image.merge(original_mode, inverted_bands)
        
        # Save the inverted image
        inverted_img.save(image_path)
        print(f"Inverted {image_path}")

def main():
    # Get all png files in current directory
    png_files = [f for f in os.listdir('.') if f.lower().endswith('.png')]
    
    if not png_files:
        print("No png files found in the current directory.")
        return
        
    print(f"Found {len(png_files)} PNG files to process...")
    
    # Process each image
    for png_file in png_files:
        try:
            invert_image(png_file)
        except Exception as e:
            print(f"Error processing {png_file}: {str(e)}")
    
    print("Processing complete!")

if __name__ == "__main__":
    main()
