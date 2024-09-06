import os
import requests
import json
from github import Github

# Set up GitHub API client
github_token = os.getenv('GITHUB_TOKEN')
openai_api_key = os.getenv('OPENAI_API_KEY')
g = Github(github_token)
repo_name = os.getenv('GITHUB_REPOSITORY')

# Load the event data to get the pull request number
with open(os.getenv('GITHUB_EVENT_PATH')) as f:
    event_data = json.load(f)

pr_number = event_data['pull_request']['number']

repo = g.get_repo(repo_name)
pull_request = repo.get_pull(pr_number)
files = pull_request.get_files()

pr_details = f"PR Title: {pull_request.title}\nPR Body: {pull_request.body}"
file_changes = "\n".join([f.filename for f in files])
diff = "\n\n".join([f.patch for f in files if f.patch])

request_body = {
    "model": "gpt-4",
    "messages": [
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": f"Please review the following pull request changes for a .NET Core project:\n\n{pr_details}\n\nFiles changed:\n{file_changes}\n\nDiff:\n{diff}\n\nReview each file change and provide line-specific comments where necessary, including any errors or potential improvements."}
    ],
    "max_tokens": 2000
}

headers = {
    "Content-Type": "application/json",
    "Authorization": f"Bearer {openai_api_key}"
}

response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, data=json.dumps(request_body))

if response.status_code == 200:
    comments = response.json()['choices'][0]['message']['content'].strip()

    if comments:
        review_comments = []

        for file in files:
            # For simplicity, we'll assume comments are general for now
            # This could be enhanced to parse line-specific comments from the response
            if file.patch:  # Ensure the patch is not empty
                lines = file.patch.split('\n')
                for i, line in enumerate(lines):
                    if line.startswith('+') and not line.startswith('+++'):
                        review_comment = {
                            "path": file.filename,
                            "position": i + 1,  # Position in the diff
                            "body": comments
                        }
                        review_comments.append(review_comment)
                        break

        pull_request.create_review(
            body="Automated review by GPT-4",
            event="COMMENT",
            comments=review_comments
        )
else:
    print(f"Error: {response.status_code}, {response.text}")
