// Each entry defines a route, a sidebar label, and its source in the repository.
export const sections = [
  {
    label: 'User guides',
    pages: [
      ['use/learner-guide', 'Learner guide', 'Docs/LEARNER_GUIDE.md'],
      ['use/desktop-webgl', 'Desktop WebGL', 'Docs/WEB_DESKTOP.md'],
      ['use/narration', 'Panel narration', 'Docs/TTS.md'],
    ],
  },
  {
    label: 'Development',
    pages: [
      ['develop/setup', 'Development setup', 'Docs/SETUP.md'],
      ['develop/manual', 'Developer manual', 'Docs/DEVELOPER_MANUAL.md'],
      ['develop/architecture', 'Architecture', 'Docs/ARCHITECTURE.md'],
      ['develop/bone-inspection', 'G3 bone inspection', 'Docs/G3_INSPECTION.md'],
      ['develop/bone-groups', 'Add a bone group', 'Docs/ADDING_BONE_GROUPS.md'],
    ],
  },
  {
    label: 'Operations',
    pages: [
      ['operate/educator-portal', 'Educator portal', 'Docs/EDUCATOR_PORTAL.md'],
      ['operate/learner-accounts', 'Learner accounts', 'Docs/USER_ACCOUNTS.md'],
      ['operate/quiz', 'Quiz integration', 'Docs/QUIZ_INTEGRATION.md'],
      ['operate/quiz-ai', 'Quiz AI pipeline', 'Docs/QUIZ_AI_PIPELINE.md'],
      ['operate/aws', 'AWS architecture', 'Docs/AWS_ARCHITECTURE.md'],
      ['operate/access', 'Accounts and access', 'Docs/ACCOUNTS.md'],
      ['operate/program-catalog', 'MMSU program catalog', 'Docs/MMSU_PROGRAM_CATALOG.md'],
    ],
  },
  {
    label: 'Release status',
    pages: [
      ['release/handoff', 'Project handoff', 'Docs/RELEASE_HANDOFF.md'],
      ['release/known-issues', 'Known issues', 'Docs/KNOWN_ISSUES.md'],
      ['release/quest', 'Quest 1.2 follow-up', 'Docs/VR_1_2_FOLLOWUP.md'],
      ['release/quiz-audit', 'Quiz content audit', 'Docs/QUIZ_CONTENT_AUDIT_2026-11-06.md'],
    ],
  },
];
