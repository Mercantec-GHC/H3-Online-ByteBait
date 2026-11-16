// Opretter fredagens release, når release-PR'en fra dev er merget til main.
// Versionen tages fra input (workflow_dispatch) eller fra den åbne sprint-milestone
// med den tidligste deadline, fx "Sprint 1 – Hele kæden (v0.1.0)".
// Sætter tag + GitHub Release med autogenererede release notes og lukker milestonen.

module.exports = async ({ github, context, core }) => {
  const { owner, repo } = context.repo;
  core.setOutput('created', 'false');

  const milestones = await github.paginate(github.rest.issues.listMilestones, { owner, repo, state: 'open', per_page: 100 });
  const sprint = milestones
    .filter((m) => /^Sprint \d/.test(m.title) && /\(v\d+\.\d+\.\d+\)/.test(m.title))
    .sort((a, b) => (a.due_on || '').localeCompare(b.due_on || ''))[0];

  let version = (process.env.VERSION || '').trim();
  if (!version && sprint) version = sprint.title.match(/\((v\d+\.\d+\.\d+)\)/)[1];
  if (!/^v\d+\.\d+\.\d+$/.test(version)) {
    core.warning('Ingen version fundet (hverken input eller åben sprint-milestone med "(vX.Y.Z)"). Ingen release oprettet.');
    return;
  }

  try {
    await github.rest.git.getRef({ owner, repo, ref: `tags/${version}` });
    core.notice(`Tag ${version} findes allerede – ingen ny release.`);
    return;
  } catch (e) {
    if (e.status !== 404) throw e;
  }

  const title = sprint && sprint.title.includes(version) ? `${version} – ${sprint.title.replace(/ \(v.*\)$/, '')}` : version;
  const rel = await github.rest.repos.createRelease({
    owner, repo, tag_name: version, target_commitish: context.sha, name: title,
    generate_release_notes: true, make_latest: 'true',
  });
  core.notice(`Release oprettet: ${rel.data.html_url}`);

  if (sprint && sprint.title.includes(version)) {
    await github.rest.issues.updateMilestone({ owner, repo, milestone_number: sprint.number, state: 'closed' });
    core.info(`Milestone lukket: ${sprint.title}`);
  }

  core.setOutput('created', 'true');
  await core.summary.addHeading(`Release ${version}`).addLink(rel.data.html_url, rel.data.html_url).write();
};
