"""DynamoDB persistence for one active practice session per learner.

Table schema: learner_id (String partition key). The session payload is JSON;
revision is a separate Number used to reject concurrent writes.
"""

import json


class DynamoQuizStore:
    def __init__(self, table_name):
        import boto3

        self.table = boto3.resource("dynamodb").Table(table_name)

    def get(self, learner_id):
        response = self.table.get_item(
            Key={"learner_id": learner_id}, ConsistentRead=True
        )
        item = response.get("Item")
        return json.loads(item["payload"]) if item else None

    def create(self, learner_id, state):
        try:
            self.table.put_item(
                Item={
                    "learner_id": learner_id,
                    "revision": state["revision"],
                    "payload": json.dumps(state, allow_nan=False),
                },
                ConditionExpression="attribute_not_exists(learner_id)",
            )
            return True
        except Exception as error:
            if self._is_condition_failure(error):
                return False
            raise

    def replace(self, learner_id, state, expected_revision):
        try:
            self.table.update_item(
                Key={"learner_id": learner_id},
                UpdateExpression="SET #rev = :new, #payload = :payload",
                ConditionExpression="#rev = :old",
                ExpressionAttributeNames={"#rev": "revision", "#payload": "payload"},
                ExpressionAttributeValues={
                    ":new": state["revision"],
                    ":old": expected_revision,
                    ":payload": json.dumps(state, allow_nan=False),
                },
            )
            return True
        except Exception as error:
            if self._is_condition_failure(error):
                return False
            raise

    @staticmethod
    def _is_condition_failure(error):
        return (
            getattr(error, "response", {}).get("Error", {}).get("Code")
            == "ConditionalCheckFailedException"
        )
