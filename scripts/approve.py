#!/usr/bin/env python3
"""Operator command. Do not expose this command as an MCP tool."""
import argparse
import json
import os
import re
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

parser = argparse.ArgumentParser(description='Approve one fictional order cancellation for five minutes.')
parser.add_argument('directory', type=Path)
parser.add_argument('order_id')
args = parser.parse_args()
if not re.fullmatch(r'[A-Za-z0-9-]{1,64}', args.order_id):
    parser.error('Order ID must contain 1–64 ASCII letters, digits or hyphens.')
root = args.directory.resolve()
root.mkdir(parents=True, exist_ok=True)
approval = {
    'action': 'cancel_order',
    'orderId': args.order_id,
    'expiresAt': (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat(),
}
fd, temporary = tempfile.mkstemp(prefix='.approval-', dir=root)
try:
    with os.fdopen(fd, 'w', encoding='utf-8') as stream:
        json.dump(approval, stream)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, root / (args.order_id + '.json'))
finally:
    if os.path.exists(temporary):
        os.unlink(temporary)
print(f'Approved cancel_order for {args.order_id} until {approval["expiresAt"]}')
