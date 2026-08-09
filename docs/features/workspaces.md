# Workspaces

**A dropdown at the top of the sidebar. Read this before you build anything around it.**

There is no Workspaces page. The entire feature is the select box directly under the Prism logo
in the sidebar, above the navigation list.

---

## What it does

The dropdown lists every workspace by name. Selecting one stores your choice in the browser, so
it survives a reload, and the active workspace's description appears in small text underneath.

Prism ships with one workspace, **Default Workspace**, described as "Your default research
workspace. All projects and data live here." It is selected automatically on first load.

That is the complete behaviour.

---

## What it does not do

**Switching workspaces does not filter anything.** Projects, experiments, datasets and prompt
templates are all fetched and displayed without reference to the active workspace. Change the
dropdown and every page shows exactly what it showed before.

**You cannot create, rename, delete or recolour a workspace from the app.** There is no dialog
and no settings screen. Workspaces carry a name, a description, a default flag and an icon
colour, and none of the four is editable here.

Creating one through the API is possible and pointless. It will appear in the dropdown, you will
be able to select it, and selecting it will change nothing you can see. Projects have a
workspace field in the database that nothing populates and nothing reads.

---

## What to do instead

Today this is a placeholder for scoping that has not been implemented. For it to earn its place
in the sidebar it would need to filter at least projects, datasets and experiments by the active
workspace, and creating a project while a workspace is active would need to file it there.

Until that exists, group your work with the tools that do function: put a prefix or a study name
in your project names on the [Experiments](experiments.md) page, and use tags on prompt templates
in the [Prompt Lab](prompt-lab.md). Neither is as tidy as a real workspace, and both actually
filter.

Leave the dropdown on **Default Workspace**. Nothing depends on it.

---

## See also

- [Experiments](experiments.md) — projects, which are the grouping that works
- [Datasets](datasets.md)
