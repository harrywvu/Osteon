"""Inspect a deployed quiz ZIP and optionally import its exact question bank.

Run with --apply only after a backup and a readback of the deployed Lambda ZIP.
"""
import argparse
import csv
import hashlib
import io
import json
import os
import sys
import uuid
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "quiz-api"))
import question_bank


def from_zip(path):
    with zipfile.ZipFile(path) as archive:
        source = archive.read("assets/questions_datasets.csv")
        model = archive.read("assets/random_forest_mastery_model.onnx")
    rows = list(csv.DictReader(io.StringIO(source.decode("utf-8-sig"))))
    return rows, hashlib.sha256(source + model).hexdigest()[:16], hashlib.sha256(model).hexdigest()


def seed(db, version, bank, activate=False):
    digest = hashlib.sha256(json.dumps(bank, sort_keys=True).encode()).hexdigest()
    manifest_key = {"pk": "VERSION#" + version, "sk": "META"}
    manifest = db.get_item(Key=manifest_key, ConsistentRead=True).get("Item")
    if manifest and manifest["digest"] != digest:
        raise RuntimeError("legacy version conflicts with an existing manifest")
    if not manifest:
        with db.batch_writer() as batch:
            for order, row in enumerate(bank):
                batch.put_item(Item={"pk": "VERSION#" + version,
                                     "sk": "QUESTION#" + row["question_id"], "order": order,
                                     "body": json.dumps(row, ensure_ascii=False)})
        db.put_item(Item={**manifest_key, "count": len(bank), "digest": digest,
                          "educator": "migration", "published_at": 0},
                    ConditionExpression="attribute_not_exists(pk)")
    # Never activate a version until all question rows can be read back.
    question_bank.rows.cache_clear()
    if list(question_bank.rows(version)) != bank:
        raise RuntimeError("legacy bank readback does not match the deployed ZIP")
    if activate:
        active = db.get_item(Key={"pk": "ACTIVE", "sk": "META"}, ConsistentRead=True).get("Item")
        if active and active["version"] != version:
            raise RuntimeError("bank already has a different active version")
        if not active:
            db.put_item(Item={"pk": "ACTIVE", "sk": "META", "version": version},
                        ConditionExpression="attribute_not_exists(pk)")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--live-zip", required=True, type=Path)
    parser.add_argument("--historical-zip", action="append", type=Path, default=[])
    parser.add_argument("--table", required=True)
    parser.add_argument("--reviewed-csv", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    live, legacy_hash, live_model = from_zip(args.live_zip)
    if len(live) != 50 or len({row["question_id"] for row in live}) != 50:
        raise ValueError("live ZIP does not contain the expected 50 unique questions")
    legacy_version = "legacy-" + legacy_hash
    historical = []
    legacy_map = {legacy_hash: legacy_version}
    for path in args.historical_zip:
        bank, content_hash, model_hash = from_zip(path)
        if model_hash != live_model:
            raise ValueError("historical model differs; pinning requires an archived model")
        if len(bank) != 50 or len({row["question_id"] for row in bank}) != 50:
            raise ValueError("historical ZIP does not contain 50 unique questions")
        if content_hash not in legacy_map:
            version = "legacy-" + content_hash
            historical.append((version, bank))
            legacy_map[content_hash] = version
    print(json.dumps({"live_questions": len(live), "legacy_content_version": legacy_hash,
                      "legacy_bank_version": legacy_version,
                      "legacy_bank_map": legacy_map,
                      "local_csv_differs": live != list(question_bank.packaged_rows())}, indent=2))
    reviewed = None
    if args.reviewed_csv:
        with args.reviewed_csv.open(newline="", encoding="utf-8-sig") as source:
            reviewed = list(csv.DictReader(source))
        question_bank.validate(reviewed, live)
    if not args.apply:
        print("Dry run only. No AWS writes.")
        return
    os.environ["QUESTION_BANK_TABLE_NAME"] = args.table
    db = question_bank.table()
    for version, bank in historical:
        seed(db, version, bank)
        print("Imported historical bank:", version)
    seed(db, legacy_version, live, activate=True)
    print("Imported legacy bank:", legacy_version)
    if reviewed and reviewed != live:
        request_id = str(uuid.uuid5(uuid.NAMESPACE_URL, hashlib.sha256(args.reviewed_csv.read_bytes()).hexdigest()))
        published = question_bank.publish(legacy_version, reviewed, request_id, "migration")
        print("Published reviewed bank:", published)


if __name__ == "__main__":
    main()
