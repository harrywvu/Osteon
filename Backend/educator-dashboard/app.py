"""Local, read-only educator view of the packaged quiz and learner records."""

import argparse
import csv
import html
import json
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit


QUESTIONS = Path(__file__).resolve().parents[1] / "quiz-api" / "assets" / "questions_datasets.csv"


def questions():
    with QUESTIONS.open(newline="", encoding="utf-8-sig") as source:
        return list(csv.DictReader(source))


def scan_all(table, **kwargs):
    items = []
    cursor = None
    while True:
        page = table.scan(**kwargs, **({"ExclusiveStartKey": cursor} if cursor else {}))
        items.extend(page.get("Items", []))
        cursor = page.get("LastEvaluatedKey")
        if not cursor:
            return items


def learner_profiles(accounts_table, quiz_table):
    # Projection prevents account codes, sessions, and encrypted replay records
    # from entering this process even though the table must be scanned.
    from boto3.dynamodb.conditions import Attr

    profiles = scan_all(
        accounts_table,
        FilterExpression=Attr("pk").begins_with("PROFILE#"),
        ProjectionExpression="pk, learner_id, profile_label, #status, created_at",
        ExpressionAttributeNames={"#status": "status"},
    )
    progress = {
        item["learner_id"]: json.loads(item["payload"])
        for item in scan_all(quiz_table, ProjectionExpression="learner_id, payload")
    }
    return sorted(
        [{"profile": profile, "progress": progress.get(profile["learner_id"])}
         for profile in profiles],
        key=lambda row: (str(row["profile"].get("profile_label", "")), row["profile"]["learner_id"]),
    )


def esc(value):
    return html.escape(str(value if value is not None else ""), quote=True)


def page(title, body):
    return f"""<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{esc(title)} | Osteon Educator</title>
<style>
body{{font:16px/1.5 system-ui,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#17212d;background:#f7f9fb}}
nav a{{margin-right:1.5rem}} a{{color:#07549c}} table{{border-collapse:collapse;width:100%;background:white}}
th,td{{border:1px solid #d1dce5;padding:.6rem;text-align:left;vertical-align:top}}
th{{background:#e9f2f8}} tr:nth-child(even){{background:#f6fafc}}
section{{background:white;border:1px solid #d1dce5;padding:1rem;margin:1rem 0}}
input{{font:inherit;padding:.4rem}} .muted{{color:#536375}} .correct{{font-weight:bold;color:#126737}}
</style><h1>Osteon Educator</h1><nav><a href="/questions">Question bank</a><a href="/learners">Learners</a></nav>
<main><h2>{esc(title)}</h2>{body}</main></html>"""


def question_page(query):
    rows = questions()
    term = query.strip().casefold()
    if term:
        rows = [row for row in rows if term in " ".join(row.values()).casefold()]
    cards = []
    for row in rows:
        options = "".join(
            f'<li class="{"correct" if letter == row["correct_option"].strip().upper() else ""}">'
            f'{letter}. {esc(row[f"option_{letter.lower()}"])}'
            f'{" (correct)" if letter == row["correct_option"].strip().upper() else ""}</li>'
            for letter in "ABCD"
        )
        cards.append(f'<section><h3>{esc(row["question_id"])}: {esc(row["question_text"])}</h3>'
                     f'<p>{esc(row["bone_group"])} / {esc(row["bone_name"])} · '
                     f'Difficulty: {esc(row["difficulty_level"])} '
                     f'(score {esc(row["difficulty_score"])})</p><ol style="list-style:none;padding:0">{options}</ol></section>')
    form = f'<form><label>Search questions <input name="q" value="{esc(query)}"></label> <button>Search</button></form>'
    return page("Question bank", f'<p class="muted">{len(rows)} of {len(questions())} packaged questions. Read only.</p>' + form + "".join(cards))


def learners_page(records):
    rows = []
    for record in records:
        profile, state = record["profile"], record["progress"]
        learner_id = profile["learner_id"]
        rows.append(f'<tr><td><a href="/learners/{esc(learner_id)}">{esc(profile.get("profile_label"))}</a>'
                    f'<br><small>{esc(learner_id)}</small></td><td>{esc(profile.get("status"))}</td>'
                    f'<td>{esc(profile.get("created_at"))}</td>'
                    f'<td>{"{:.0%}".format(float(state["mastery"])) if state else "No quiz session"}</td>'
                    f'<td>{len(state.get("asked_ids", [])) if state else 0}</td></tr>')
    body = '<p class="muted">Current saved quiz state; answer history retains only the 10 latest attempts.</p>'
    body += '<table><thead><tr><th>Profile</th><th>Status</th><th>Created (Unix time)</th><th>Mastery</th><th>Answers this run</th></tr></thead><tbody>'
    return page("Learners", body + "".join(rows) + "</tbody></table>")


def learner_page(record):
    profile, state = record["profile"], record["progress"]
    body = f'<p>ID: {esc(profile["learner_id"])} · Status: {esc(profile.get("status"))} · Created: {esc(profile.get("created_at"))} (Unix time)</p>'
    if state:
        body += (f'<p>Mastery: {float(state["mastery"]):.0%} · Answers this run: {len(state.get("asked_ids", []))} '
                 f'· Completed: {"Yes" if state.get("completed") else "No"} '
                 f'· Current question: {esc(state.get("current_question_id") or "None")}</p>')
        history = state.get("history", [])
        body += '<h3>Recent answers (up to 10)</h3><table><thead><tr><th>Question</th><th>Difficulty</th><th>Choice</th><th>Correct</th><th>Mastery after</th><th>Answered (Unix time)</th></tr></thead><tbody>'
        for item in reversed(history):
            body += (f'<tr><td>{esc(item.get("question_id"))}</td><td>{esc(item.get("difficulty"))}</td>'
                     f'<td>{esc(item.get("choice"))}</td><td>{"Yes" if item.get("correct") else "No"}</td>'
                     f'<td>{float(item.get("updated_mastery", 0)):.0%}</td><td>{esc(item.get("accepted_at"))}</td></tr>')
        body += '</tbody></table>'
    else:
        body += '<p>No quiz session yet.</p>'
    return page(profile.get("profile_label", "Learner"), body)


def make_handler(accounts_table, quiz_table, port):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            host = self.headers.get("Host", "")
            if host not in (f"127.0.0.1:{port}", f"localhost:{port}"):
                self.send_error(403)
                return
            url = urlsplit(self.path)
            try:
                if url.path in ("/", "/questions"):
                    output = question_page(parse_qs(url.query).get("q", [""])[0])
                    status = 200
                elif url.path == "/learners":
                    output = learners_page(learner_profiles(accounts_table, quiz_table))
                    status = 200
                elif url.path.startswith("/learners/"):
                    learner_id = url.path.removeprefix("/learners/")
                    record = next((r for r in learner_profiles(accounts_table, quiz_table)
                                   if r["profile"]["learner_id"] == learner_id), None)
                    status = 200 if record else 404
                    output = learner_page(record) if record else page("Not found", "<p>Unknown learner.</p>")
                else:
                    status, output = 404, page("Not found", "<p>Unknown page.</p>")
            except Exception:
                self.log_exception()
                status, output = 503, page("Data unavailable", "<p>Check AWS profile, Region, and table read permissions in the terminal.</p>")
            payload = output.encode("utf-8")
            self.send_response(status)
            for name, value in {"Content-Type": "text/html; charset=utf-8", "Content-Length": str(len(payload)),
                                "Cache-Control": "no-store", "X-Frame-Options": "DENY",
                                "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'"}.items():
                self.send_header(name, value)
            self.end_headers()
            self.wfile.write(payload)

        def log_exception(self):
            import traceback
            traceback.print_exc()

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", help="AWS CLI profile with read-only educator permissions")
    parser.add_argument("--region", default="ap-southeast-2")
    parser.add_argument("--accounts-table", default="osteon-accounts")
    parser.add_argument("--quiz-table", default="osteon-quiz-demo")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    import boto3
    session = boto3.Session(profile_name=args.profile, region_name=args.region)
    resource = session.resource("dynamodb")
    server = ThreadingHTTPServer(("127.0.0.1", args.port),
                                 make_handler(resource.Table(args.accounts_table), resource.Table(args.quiz_table), args.port))
    print(f"Educator dashboard: http://127.0.0.1:{args.port}/questions")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
