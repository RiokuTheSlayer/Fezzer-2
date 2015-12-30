"""
Extract and decompress FEZ .pak files
"""

from __future__ import print_function

import sys
import os
import time

from xnb_parse.fez_content_manager import FezContentManager


def unpack(content_dir, out_dir):
    content_manager = FezContentManager(content_dir)
    out_dir = os.path.normpath(out_dir)
    for asset_name in content_manager.assets:
        print(asset_name)
        xnb = content_manager.xnb(asset_name, parse=False)
        out_file = os.path.join(out_dir, os.path.normpath(asset_name))
        xnb.save(filename=out_file)


def main():
    if len(sys.argv) == 3:
        totaltime = time.time()
        unpack(os.path.normpath(sys.argv[1]), os.path.normpath(sys.argv[2]))
        print('> Done in {:.2f} seconds'.format(time.time() - totaltime))
    else:
        print('fez_decomp.py Content out_dir')
