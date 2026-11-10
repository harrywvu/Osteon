import importlib.util
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "quiz-api"))

import question_bank
from import_live_bank import seed

if importlib.util.find_spec("moto"):
    import boto3
    from moto import mock_aws


@unittest.skipUnless(importlib.util.find_spec("moto"), "moto is required")
class MigrationTests(unittest.TestCase):
    @mock_aws
    def test_historical_bank_stays_inactive_until_live_bank_is_complete(self):
        with patch.dict(os.environ, {"AWS_DEFAULT_REGION": "ap-southeast-2",
                                  "QUESTION_BANK_TABLE_NAME": "test-bank"}):
            question_bank.table.cache_clear()
            question_bank.rows.cache_clear()
            db = boto3.resource("dynamodb").create_table(
                TableName="test-bank",
                KeySchema=[{"AttributeName": "pk", "KeyType": "HASH"},
                           {"AttributeName": "sk", "KeyType": "RANGE"}],
                AttributeDefinitions=[{"AttributeName": "pk", "AttributeType": "S"},
                                      {"AttributeName": "sk", "AttributeType": "S"}],
                BillingMode="PAY_PER_REQUEST")
            older = list(question_bank.packaged_rows())
            live = [dict(row) for row in older]
            live[0]["question_text"] = "Updated live question?"
            seed(db, "legacy-older", older)
            self.assertIsNone(db.get_item(Key={"pk": "ACTIVE", "sk": "META"}).get("Item"))
            seed(db, "legacy-live", live, activate=True)
            self.assertEqual("legacy-live", question_bank.active_version())
            self.assertEqual(older, list(question_bank.rows("legacy-older")))
            self.assertEqual(live, list(question_bank.rows("legacy-live")))
            seed(db, "legacy-older", older)
            seed(db, "legacy-live", live, activate=True)
            question_bank.table.cache_clear()
            question_bank.rows.cache_clear()


if __name__ == "__main__":
    unittest.main()
