
# App Playground Repo

This is a share app github repo to explore application ideas by GitHub Copilot.

## Repo Guidlines

- Each project have a unique `project_id`: start with date as suffix {date}_{project desc} . e.g. `20260605_testapp`
- Each of the project must be created inside its own `project_folder` sitting in the root of the repo. the folder name is `project_id`
- Then inside the `project_folder`, it should contain a list of folders for apps and infra: `{project_id}\apps`, `{project_id}\bicep`, `{project_id}\docs`, `{project_id}\scripts`
- The app's solution design and requirements sits inside `{project_id}\docs`, it might have `todo.md`, `solution.md`, `task.md`. these documents to be updated as the solution being built out.
- The apps and infra should be deployed via github actions, these actions yml should start with `project_id` as prefix. e.g. `{project_id}_infra.yml`
- the `{project_id}\apps` might have '`{project_id}\apps\frontend`,  '`{project_id}\apps\backend`


## Full list of Projects

TODO: register each of the project here.


## Folder Stucture

TODO: update here

