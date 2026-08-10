import type { Tour } from './types'

/**
 * The walkthroughs Prism ships with.
 *
 * Two rules govern what belongs here. A step must point at something that exists on a cold
 * install, or declare a requirement so it is never offered into an empty page — Analytics
 * reads zero until traffic exists, History is blank until something has run, and a tour that
 * proudly presents an empty screen teaches the reader the tool is broken. And a step must say
 * what the reader gets, not what the button is called; "Stats panel" is not worth a tour stop,
 * "how to tell whether the GPU is actually helping" is.
 */

/** Identity of the tour shown on a first visit. */
export const WELCOME_TOUR_ID = 'welcome'

const welcomeTour: Tour = {
  id: WELCOME_TOUR_ID,
  kind: 'welcome',
  title: 'Around Prism in a minute',
  outcome: 'Know what the main areas are for and where to start.',
  minutes: 1,
  requires: [],
  steps: [
    {
      id: 'what-this-is',
      title: 'This is a workbench for looking inside a model',
      body:
        'Prism runs prompts against a local inference server and keeps what most tools throw '
        + 'away: how long the first token took, how fast the rest arrived, and how confident '
        + 'the model was in each word it chose. Everything here is built on those recordings.',
      action: 'Arrow keys move through this tour. Escape leaves it.',
    },
    {
      id: 'sidebar',
      title: 'Each area answers a different question',
      body:
        'Playground is where you talk to a model. Token Explorer is where you pull one answer '
        + 'apart. History is everything you have already run. The rest — experiments, '
        + 'datasets, evaluation — are for when one answer stops being enough.',
      anchor: 'sidebar',
      side: 'right',
    },
    {
      id: 'models',
      title: 'Nothing works until a server is connected',
      body:
        'This page finds inference servers already running on your machine and registers one. '
        + 'It also reports what each can do — in particular whether it returns per-token '
        + 'probabilities, which is what the token-level views are built from.',
      route: '/models',
      anchor: 'nav-models',
      side: 'right',
    },
    {
      id: 'playground',
      title: 'Where you actually send something',
      body:
        'The conversation sits in the middle and the sampling settings on the right — '
        + 'temperature, top-p, how many tokens to allow, and whether to ask for per-token '
        + 'probabilities. Every response is timed and recorded as it streams, so you never '
        + 'have to decide in advance that a run was worth measuring.',
      route: '/playground',
      anchor: 'playground-parameters',
      side: 'right',
    },
    {
      id: 'status',
      title: 'The bar along the bottom tells you what you have',
      body:
        'It shows the connected server and whether token probabilities are available from it. '
        + 'If the token-level views ever look empty, this is the first thing to read — it is '
        + 'usually the answer.',
      anchor: 'status-bar',
      side: 'top',
    },
    {
      id: 'guide',
      title: 'Come back here whenever',
      body:
        'This button reopens the guide. It also holds a handful of short walkthroughs that '
        + 'each take you through one real task, rather than describing the buttons.',
      anchor: 'guide-button',
      side: 'right',
      action: 'Open it when you finish and pick one.',
    },
  ],
}

const situations: Tour[] = [
  {
    id: 'first-answer',
    kind: 'situation',
    title: 'Get your first answer, and read what it cost',
    outcome:
      'A model connected, a prompt sent, and the timing behind the answer read off rather '
      + 'than guessed at.',
    minutes: 3,
    requires: [],
    steps: [
      {
        id: 'connect',
        title: 'Connect a server',
        body:
          'Prism probes the usual local ports — Ollama on 11434, vLLM on 8000, LM Studio on '
          + '1234 — and offers whatever answers. If nothing does, this page tells you exactly '
          + 'what to start.',
        route: '/models',
        anchor: 'nav-models',
        side: 'right',
        action: 'Register one, then come back with the arrow key.',
      },
      {
        id: 'ask',
        title: 'Ask it something',
        body:
          'Anything will do. A question with a few sentences of answer gives the timings '
          + 'something to measure — one-word replies are over before the numbers mean much.',
        route: '/playground',
        anchor: 'playground-composer',
        side: 'top',
        action: 'Type a prompt and press Enter.',
      },
      {
        id: 'read-the-cost',
        title: 'Now read what it cost',
        body:
          'Two numbers matter and they are not the same. Time to first token is how long the '
          + 'model spent reading your prompt before saying anything. Tokens per second is how '
          + 'fast it wrote once it started. A change to your prompt moves the first; a change '
          + 'of hardware usually moves the second.',
        anchor: 'playground-stats-toggle',
        side: 'bottom',
        action: 'Open Stats and look at TTFT and tok/s separately.',
      },
    ],
  },
  {
    id: 'why-that-word',
    kind: 'situation',
    title: 'Find out why it chose that word',
    outcome:
      'Read the model\'s confidence token by token, and see what it nearly said instead.',
    minutes: 4,
    requires: ['logprobs'],
    steps: [
      {
        id: 'turn-on-logprobs',
        title: 'Ask for the probabilities',
        body:
          'The model can report how likely each token was, and which alternatives it passed '
          + 'over. That is off by default because it makes responses slightly larger, so turn '
          + 'it on before the run you want to inspect.',
        route: '/playground',
        anchor: 'playground-parameters',
        side: 'right',
        action: 'Enable logprobs in the parameters, then send a prompt.',
      },
      {
        id: 'read-the-heatmap',
        title: 'Read the answer by confidence',
        body:
          'Each token is shaded by how sure the model was. The pale ones are where it was '
          + 'nearly guessing — which is usually where a wrong answer starts, and where a '
          + 'clearer prompt has the most to fix.',
        anchor: 'playground-logprobs-toggle',
        side: 'bottom',
      },
      {
        id: 'explore-branches',
        title: 'Then take one apart',
        body:
          'Token Explorer steps through generation one token at a time and shows the '
          + 'alternatives at each point, so you can see the fork the answer turned on instead '
          + 'of inferring it from the finished text.',
        route: '/token-explorer',
        anchor: 'nav-token-explorer',
        side: 'right',
      },
    ],
  },
  {
    id: 'compare-servers',
    kind: 'situation',
    title: 'Settle whether one server is really faster',
    outcome:
      'A measured answer to "is the GPU actually helping", instead of an impression.',
    minutes: 5,
    requires: ['provider'],
    steps: [
      {
        id: 'open-compare',
        title: 'Put them side by side',
        body:
          'Compare runs the same prompt against up to four registered servers at once. Sending '
          + 'it once to both is the only way to compare them fairly — two separate runs differ '
          + 'by prompt, by cache state, and by whatever else changed in between.',
        route: '/playground/compare',
        anchor: 'main',
        side: 'bottom',
        action: 'Point each pane at a different server and send one prompt.',
      },
      {
        id: 'read-the-split',
        title: 'Read the two numbers apart',
        body:
          'The comparison strip reports time to first token and decode throughput separately, '
          + 'and never averages them into one score. On Apple Silicon that distinction is the '
          + 'whole finding: the CPU and GPU share memory bandwidth, so tokens per second can '
          + 'come out nearly identical while time to first token differs several times over.',
        anchor: 'main',
        side: 'top',
        action: 'Check how many responses each average covers — one sample is noise.',
      },
    ],
  },
  {
    id: 'improve-a-prompt',
    kind: 'situation',
    title: 'Improve a prompt without guessing',
    outcome: 'Two versions of a prompt tested against the same inputs, side by side.',
    minutes: 5,
    requires: ['provider'],
    steps: [
      {
        id: 'write-a-template',
        title: 'Make the prompt a thing you can version',
        body:
          'Prompt Lab keeps a prompt as a template with {{variables}} and an append-only '
          + 'version history, so "the new wording is better" becomes a claim you can check '
          + 'rather than remember. Versions are how you edit: the prompt view itself is '
          + 'read-only, and New Version pre-fills from whichever one you are looking at.',
        route: '/prompt-lab',
        anchor: 'nav-prompt-lab',
        side: 'right',
      },
      {
        id: 'save-the-inputs',
        title: 'Pin the inputs before you change the prompt',
        body:
          'Input Sets save a named bundle of variable values and reload it in one click. This '
          + 'is the part that makes the comparison honest — trying a rewrite on whatever '
          + 'example is at hand is how prompts get worse while feeling better. It is a small '
          + 'ghost button above the variables and the saved list starts collapsed, so it is '
          + 'easy to never find.',
        anchor: 'main',
        side: 'bottom',
      },
      {
        id: 'diff-the-versions',
        title: 'Then read what actually changed',
        body:
          'With more than one version saved, Diff shows them line by line. Compare runs the '
          + 'current version against several servers at once — note that is one prompt across '
          + 'many models, run one after another, rather than two prompts against one model.',
        anchor: 'main',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'look-back',
    kind: 'situation',
    title: 'Find the run you have already done',
    outcome: 'Any past call found, read in full, and sent again.',
    minutes: 2,
    requires: ['history'],
    steps: [
      {
        id: 'browse',
        title: 'Everything is already recorded',
        body:
          'Every call from every part of Prism lands here — playground, experiments, batches — '
          + 'with its prompt, its response, its timings and its token counts. Nothing had to '
          + 'be marked as worth keeping beforehand.',
        route: '/history',
        anchor: 'nav-history',
        side: 'right',
      },
      {
        id: 'replay',
        title: 'Run it again, against something else',
        body:
          'Replay re-runs a recorded call without leaving the panel, and shows the two '
          + 'responses word-diffed with a table of what each metric did. Open the parameter '
          + 'overrides and it becomes a controlled experiment: identical prompt, one value '
          + 'changed, the difference attributable to that value and nothing else.',
        anchor: 'main',
        side: 'bottom',
        action: 'Open a row, then Replay. The overrides section starts collapsed.',
      },
      {
        id: 'tags',
        title: 'The tags are clickable',
        body:
          'Tag a run from the detail panel and the badge in the table becomes a filter for it. '
          + 'Tag ten runs "baseline" and one click pulls that cohort back out — which is the '
          + 'whole of ad-hoc cohorting, and nothing on the page suggests the badges do '
          + 'anything.',
        anchor: 'main',
        side: 'bottom',
      },
    ],
  },
]

/**
 * One tour per area, offered the first time you arrive there.
 *
 * These are shorter than the situations on purpose. A situation is a task you set out to do;
 * an area tour is what you want when you have just clicked something in the sidebar and are
 * looking at a screen you have never seen. Three or four stops, then out of the way.
 */
const pageTours: Tour[] = [
  {
    id: 'page-playground',
    kind: 'page',
    area: '/playground',
    title: 'Playground',
    outcome: 'Send prompts, and read what each answer cost.',
    minutes: 2,
    requires: ['provider'],
    steps: [
      {
        id: 'compose',
        title: 'Where you talk to the model',
        body:
          'Type here and press Enter. The conversation is stored, so it is still here after a '
          + 'reload and shows up on the History page with everything else you have run.',
        route: '/playground',
        anchor: 'playground-composer',
        side: 'top',
      },
      {
        id: 'parameters',
        title: 'The settings that change the answer',
        body:
          'Temperature, top-p and top-k on the right decide how the next token is picked. The '
          + 'logprobs switch is the one worth knowing about: turn it on and the model reports '
          + 'how sure it was about each word, which is what the heatmap and Token Explorer '
          + 'are built from.',
        anchor: 'playground-parameters',
        side: 'left',
      },
      {
        id: 'stats',
        title: 'What the answer cost',
        body:
          'Time to first token is how long the model spent reading your prompt. Tokens per '
          + 'second is how fast it wrote once it started. They move for different reasons, so '
          + 'they are reported separately rather than averaged into one number.',
        anchor: 'playground-stats-toggle',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-token-explorer',
    kind: 'page',
    area: '/token-explorer',
    title: 'Token Explorer',
    outcome: 'See the distribution a token was chosen from, and force a different one.',
    minutes: 3,
    requires: ['logprobs'],
    steps: [
      {
        id: 'controls',
        title: 'A prompt, and what to do with it',
        body:
          'Pick a server and write a prompt on the left. Everywhere else in Prism shows you '
          + 'what the model did say; this page shows the distribution it was sampling from when '
          + 'it decided. It needs a server that reports token probabilities, and says so plainly '
          + 'if the one you picked does not.',
        route: '/token-explorer',
        anchor: 'token-explorer-controls',
        side: 'right',
        action: 'Write a prompt and press Predict.',
      },
      {
        id: 'click-a-bar',
        title: 'The bars are buttons',
        body:
          'Clicking a candidate token does not just select it. In Predictions it forces that '
          + 'token and generates a whole continuation from there, filed under Branches with a '
          + 'perplexity score, so you can read the two futures side by side. In Step Through '
          + 'the same chart forces the token and carries on one step at a time.',
        anchor: 'token-explorer-views',
        side: 'bottom',
      },
      {
        id: 'sampling',
        title: 'The sliders here only redraw the picture',
        body:
          'Temperature is sent to the model, but the top-p and top-k sliders re-shade the '
          + 'chart you already have — they show you which candidates a given setting would '
          + 'have kept, without spending another request to find out.',
        anchor: 'token-explorer-sampling',
        side: 'left',
      },
    ],
  },
  {
    id: 'page-models',
    kind: 'page',
    area: '/models',
    title: 'Models',
    outcome: 'Know what is connected, and what each server can and cannot do.',
    minutes: 2,
    requires: ['provider'],
    steps: [
      {
        id: 'what-for',
        title: 'Everything else reads from what is registered here',
        body:
          'A server has to be registered before any other page can use it. Prism probes the '
          + 'conventional local ports, works out what kind of server answered, and keeps what '
          + 'it learned rather than asking again on every request.',
        route: '/models',
        anchor: 'models-instances',
        side: 'bottom',
      },
      {
        id: 'capabilities',
        title: 'Providers are not interchangeable',
        body:
          'The matrix is the honest answer to "why is that view empty". Per-token '
          + 'probabilities, guided decoding and model swapping are all things a given server '
          + 'either does or does not do, and the pages that need them stay blank without.',
        anchor: 'models-capabilities',
        side: 'bottom',
      },
      {
        id: 'detail',
        title: 'Probe rather than trust',
        body:
          'Selecting a server shows what it reported and lets you re-probe it. Worth doing '
          + 'after updating the server itself: capabilities are recorded when it is '
          + 'registered, so an upgrade that adds a feature will not be noticed until you ask.',
        anchor: 'models-detail',
        side: 'left',
      },
    ],
  },
  {
    id: 'page-history',
    kind: 'page',
    area: '/history',
    title: 'History',
    outcome: 'Find any past call, and re-run it as a controlled experiment.',
    minutes: 2,
    requires: ['history'],
    steps: [
      {
        id: 'everything-lands-here',
        title: 'Every call from every page is already here',
        body:
          'Playground, Token Explorer, Prompt Lab, experiments, agents, batches — all of it, '
          + 'with the full request and response, token counts and timings. Nothing had to be '
          + 'marked as worth keeping first.',
        route: '/history',
        anchor: 'history-results',
        side: 'top',
      },
      {
        id: 'filters',
        title: 'Narrowing down',
        body:
          'Filter by the part of Prism that made the call, by model, by date or by tag. One '
          + 'oddity worth knowing: every filter applies as you change it except the search '
          + 'box, which waits for Enter or Apply.',
        anchor: 'history-filters',
        side: 'bottom',
      },
      {
        id: 'tags-are-filters',
        title: 'Tags, and one caveat',
        body:
          'Tag runs from the detail panel to group them — "baseline", "after the rewrite". The '
          + 'badges in the table are clickable and are meant to filter by that tag, but tag '
          + 'filtering currently fails on the server, so use the search box for now. Dates and '
          + 'the module filter work.',
        anchor: 'history-results',
        side: 'top',
      },
    ],
  },
  {
    id: 'page-prompt-lab',
    kind: 'page',
    area: '/prompt-lab',
    title: 'Prompt Lab',
    outcome: 'Version a prompt, and re-test it against the same inputs every time.',
    minutes: 3,
    requires: ['provider'],
    steps: [
      {
        id: 'templates',
        title: 'A prompt with variables and a history',
        body:
          'Templates hold {{variables}}, an optional system prompt and few-shot examples, and '
          + 'keep every version you save.',
        route: '/prompt-lab',
        anchor: 'prompt-lab-templates',
        side: 'right',
      },
      {
        id: 'read-only',
        title: 'You do not edit the prompt — you add a version',
        body:
          'The prompt view is read-only, which surprises everyone. Changes go through New '
          + 'Version, which pre-fills from whichever version you are looking at, so the old '
          + 'wording survives and Diff can show you exactly what moved.',
        anchor: 'prompt-lab-templates',
        side: 'right',
      },
      {
        id: 'input-sets',
        title: 'Save the inputs, not just the prompt',
        body:
          'Input Sets store a named bundle of variable values you can reload in one click — '
          + 'the difference between comparing two versions and comparing two versions on two '
          + 'different examples. Compare runs one version across several servers, one after '
          + 'another, rather than two versions against one.',
        anchor: 'prompt-lab-test',
        side: 'left',
      },
    ],
  },
  {
    id: 'page-experiments',
    kind: 'page',
    area: '/experiments',
    title: 'Experiments',
    outcome: 'Keep a hypothesis next to the runs that tested it, and diff them.',
    minutes: 3,
    requires: ['provider'],
    steps: [
      {
        id: 'structure',
        title: 'Projects hold experiments, experiments hold runs',
        body:
          'An experiment carries a written hypothesis, so what you were trying to find out is '
          + 'stored beside the numbers rather than remembered. Runs are not a page — they are '
          + 'rows inside an experiment that open a panel when selected.',
        route: '/experiments',
        anchor: 'experiments-projects',
        side: 'bottom',
      },
      {
        id: 'compare',
        title: 'Comparing runs is a diff, not a side-by-side',
        body:
          'Tick two or more runs and Compare strips out every parameter they shared, leaving '
          + 'only what actually differed. Twelve identical settings collapse to nothing and the '
          + 'one you changed is left on screen by itself.',
        anchor: 'experiments-projects',
        side: 'bottom',
      },
      {
        id: 'sweep',
        title: 'Sweeps make the runs for you',
        body:
          'Add several values for temperature or top-p and it runs the whole grid, one '
          + 'recorded run per combination, named and tagged. It tells you the count before you '
          + 'commit — worth reading, because the runs execute one after another rather than in '
          + 'parallel.',
        anchor: 'experiments-filters',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-datasets',
    kind: 'page',
    area: '/datasets',
    title: 'Datasets',
    outcome: 'Turn a file into a versioned dataset with reproducible splits.',
    minutes: 3,
    requires: [],
    steps: [
      {
        id: 'what-a-dataset-is',
        title: 'A file with a schema, splits and a record browser',
        body:
          'Upload CSV, JSON or JSONL and Prism infers the columns and what each is for. '
          + 'Evaluation, batch inference and fine-tuning all refer back to a dataset here, so '
          + 'this is upstream of all three.',
        route: '/datasets',
        anchor: 'datasets-list',
        side: 'bottom',
      },
      {
        id: 'splits',
        title: 'Splits are seeded, so they are reproducible',
        body:
          'Splitting by ratio is reproducible when you give it a seed — the field is optional, '
          + 'and without one the assignment differs each time you split. Export honours whichever '
          + 'split you are filtered to, so pulling out just the test set is two clicks.',
        anchor: 'datasets-list',
        side: 'bottom',
      },
      {
        id: 'validation',
        title: 'The Statistics tab checks your data first',
        body:
          'Open a dataset and the Statistics tab runs a validation pass above the charts, '
          + 'flagging problems as errors, warnings or notes — which is how you notice a label '
          + 'column full of nulls before you spend an evaluation on it. Nothing on the other '
          + 'tabs hints it is there.',
        anchor: 'datasets-list',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-evaluation',
    kind: 'page',
    area: '/evaluation',
    title: 'Evaluation',
    outcome: 'Understand what this page scores, and how results get here.',
    minutes: 2,
    requires: [],
    steps: [
      {
        id: 'what-it-scores',
        title: 'Scoring model output against your own data',
        body:
          'An evaluation runs a dataset through one or more models and scores each answer '
          + 'against the expected value. The scorers are exact match, contains, BLEU, ROUGE-L, '
          + 'length ratio, and an LLM judge that grades with a model rather than string '
          + 'overlap.',
        route: '/evaluation',
        anchor: 'evaluation-tabs',
        side: 'bottom',
      },
      {
        id: 'leaderboard',
        title: 'The leaderboard spans evaluations',
        body:
          'The second tab is not a view of one evaluation — it ranks results from different '
          + 'evaluation runs against each other, naming which run each row came from. It is '
          + 'behind a tab that is not the default, so it is easy to miss entirely.',
        anchor: 'evaluation-tabs',
        side: 'bottom',
      },
      {
        id: 'how-to-start-one',
        title: 'Starting one',
        body:
          'New Evaluation asks for a dataset, the models to compare and the scorers to apply. '
          + 'It tells you before you commit that the run is every record through every model, '
          + 'plus a judging call per answer if you pick the LLM judge — which is the part that '
          + 'costs real time on a local server.',
        anchor: 'evaluation-tabs',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-batch',
    kind: 'page',
    area: '/batch',
    title: 'Batch Inference',
    outcome: 'Know what the job controls do, and which one matters most.',
    minutes: 2,
    requires: [],
    steps: [
      {
        id: 'what-a-job-is',
        title: 'One model over a whole dataset',
        body:
          'Each job runs a model across every record of a dataset at a set concurrency, '
          + 'tracking how many are done, how many failed and how many tokens went in. '
          + 'Concurrency belongs to the job, so two jobs can be moving at different rates by '
          + 'design.',
        route: '/batch',
        anchor: 'batch-jobs',
        side: 'bottom',
      },
      {
        id: 'controls',
        title: 'Pause actually resumes',
        body:
          'Pausing does not throw the work away — resuming carries on from the records already '
          + 'completed. The one to know about is Retry failed, which re-runs only the failed '
          + 'records of a finished job. It is an unlabelled circular arrow that appears only '
          + 'when a job has failures, and on a long run it is the most valuable control here.',
        anchor: 'batch-filters',
        side: 'bottom',
      },
      {
        id: 'how-to-start-one',
        title: 'Starting one',
        body:
          'New Batch Job takes a dataset, a model and a concurrency. Token probabilities are '
          + 'offered only when a registered server returns them, and are off by default: over a '
          + 'whole dataset that setting is the difference between a modest result set and a very '
          + 'large one. The list refreshes itself while anything is running.',
        anchor: 'batch-filters',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-analytics',
    kind: 'page',
    area: '/analytics',
    title: 'Analytics',
    outcome: 'See the distribution behind your latency, and where tokens are going.',
    minutes: 2,
    requires: ['history'],
    steps: [
      {
        id: 'distribution',
        title: 'The only place you see a distribution',
        body:
          'Everywhere else shows one call. Here you get mean, median, p95 and p99 over the '
          + 'last thirty days — which is how you find out your p99 is several times your '
          + 'median, something an average will never tell you.',
        route: '/analytics',
        anchor: 'analytics-summary',
        side: 'bottom',
      },
      {
        id: 'by-module',
        title: 'Usage by module answers "what is burning my tokens"',
        body:
          'Because recording wraps every provider, each call is attributed to the part of '
          + 'Prism that made it — playground, sweeps, evaluation judging, agents, replays. It '
          + 'renders as an unassuming row of boxes and is the most useful thing on the page.',
        anchor: 'analytics-tabs',
        side: 'bottom',
      },
      {
        id: 'cost-caveat',
        title: 'Cost distinguishes free from unknown',
        body:
          'The figure shown is the one the backend priced, and two different things are kept '
          + 'apart: a zero means priced and free, while "not priced" means no pricing is '
          + 'recorded for that model and nothing is being claimed. The window control at the top '
          + 'changes the period all of this covers.',
        anchor: 'analytics-tabs',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-rag',
    kind: 'page',
    area: '/rag',
    title: 'RAG Workbench',
    outcome: 'See exactly which chunks a retrieval step would hand to the model.',
    minutes: 3,
    requires: [],
    steps: [
      {
        id: 'collections',
        title: 'A collection is a corpus plus a retrieval configuration',
        body:
          'Embedding model, chunk size, overlap and distance metric are fixed per collection, '
          + 'so two collections over the same documents are a real comparison of chunking '
          + 'strategy rather than a vague impression.',
        route: '/rag',
        anchor: 'rag-collections',
        side: 'bottom',
      },
      {
        id: 'three-retrievals',
        title: 'Three ways to retrieve, and one needs no embeddings',
        body:
          'Vector search uses cosine distance, BM25 uses Postgres full-text ranking, and '
          + 'hybrid blends the two after normalising each. BM25 is computed by the database at '
          + 'ingest, so it works with no embedding server running at all. A search that fails '
          + 'now says so and points you there, and "matched nothing" is shown as its own '
          + 'result rather than looking identical to a failure.',
        anchor: 'rag-collections',
        side: 'bottom',
      },
      {
        id: 'retrieval-not-generation',
        title: 'This shows retrieval, not an answer',
        body:
          'You get the chunks with their scores and token counts — the context a RAG answer '
          + 'would have been built from — rather than the answer itself. Comparing hybrid '
          + 'scores against vector scores is not meaningful, incidentally: hybrid numbers are '
          + 'normalised and blended, not cosine similarities.',
        anchor: 'rag-collections',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-structured-output',
    kind: 'page',
    area: '/structured-output',
    title: 'Structured Output',
    outcome: 'Test whether a model reliably produces the shape your code expects.',
    minutes: 3,
    requires: ['provider'],
    steps: [
      {
        id: 'schemas',
        title: 'Schemas are saved and versioned',
        body:
          'Store a JSON Schema by name, then fire prompts at it. You get the raw text, the '
          + 'parsed object and a verdict on whether it actually conformed — the only place in '
          + 'Prism that checks output against a contract.',
        route: '/structured-output',
        anchor: 'structured-schemas',
        side: 'right',
      },
      {
        id: 'two-modes',
        title: 'Constrained, or asked nicely',
        body:
          'If the server supports guided decoding the schema is enforced during generation. If '
          + 'it does not — which includes Ollama and most OpenAI-compatible servers — Prism '
          + 'falls back to instructing the model and validating afterwards. Which mode you were '
          + 'in is reported in amber beneath the result; genuine validation failures are red. '
          + 'They are different things and no longer share a box.',
        anchor: 'structured-test',
        side: 'left',
      },
      {
        id: 'honest-validator',
        title: 'The validator admits what it cannot check',
        body:
          'Constructs like $ref, allOf and oneOf are not supported, and rather than skipping '
          + 'them quietly it reports that the result cannot be trusted either way. A pass here '
          + 'means it was actually checked.',
        anchor: 'structured-test',
        side: 'left',
      },
    ],
  },
  {
    id: 'page-agents',
    kind: 'page',
    area: '/agents',
    title: 'Agents',
    outcome: 'Read why an agent reached its answer, step by step.',
    minutes: 3,
    requires: ['provider'],
    steps: [
      {
        id: 'workflows',
        title: 'A workflow is a prompt, a model and a set of tools',
        body:
          'Running one drives a real reasoning loop against your own server: the model thinks, '
          + 'picks a tool, gets an observation back, and goes round again until it answers or '
          + 'hits the step limit.',
        route: '/agents',
        anchor: 'agents-list',
        side: 'bottom',
      },
      {
        id: 'trace',
        title: 'The trace is the point',
        body:
          'Every step is kept — which tool was called, with what input, what came back. That '
          + 'is the difference between "the agent got it wrong" and knowing which step went '
          + 'wrong. Runs are stored, so one from last week can be reopened and read.',
        anchor: 'agents-list',
        side: 'bottom',
      },
      {
        id: 'costing',
        title: 'Each step is a real call, and lands in History',
        body:
          'A five-step run is five recorded inference calls tagged as agents, so the honest '
          + 'cost of an agent is on the History and Analytics pages rather than in a single '
          + 'summary number here.',
        anchor: 'agents-list',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-fine-tuning',
    kind: 'page',
    area: '/fine-tuning',
    title: 'Fine-Tuning',
    outcome: 'Get a curated dataset into a training file — and know what this page will not do.',
    minutes: 2,
    requires: [],
    steps: [
      {
        id: 'no-training-here',
        title: 'Fine-tuning itself is not built',
        body:
          'The page says so on arrival, and this is the same message: Prism trains nothing, and '
          + 'registering an adapter records a row no inference path reads. Whether training '
          + 'belongs here is undecided. What does work is dataset export, which is why the page '
          + 'is still here at all.',
        route: '/fine-tuning',
        anchor: 'fine-tuning-tabs',
        side: 'bottom',
      },
      {
        id: 'export',
        title: 'Export is the real tool here',
        body:
          'Point it at a dataset and it writes Alpaca, ShareGPT, ChatML or OpenAI JSONL, with '
          + 'a preview and a record count — the conversion script you would otherwise write '
          + 'and get subtly wrong.',
        anchor: 'fine-tuning-tabs',
        side: 'bottom',
      },
      {
        id: 'mapping-trap',
        title: 'Check the count against your dataset',
        body:
          'The column mapping defaults to instruction / input / output. If your columns are '
          + 'named anything else, records are dropped and the number you get back is what made '
          + 'it into the file, not what was in the dataset. Also note the export takes every '
          + 'record, including your test split.',
        anchor: 'fine-tuning-mapping',
        side: 'bottom',
      },
    ],
  },
  {
    id: 'page-notebooks',
    kind: 'page',
    area: '/notebooks',
    title: 'Notebooks',
    outcome: 'Keep analysis next to the runs it analyses, as real .ipynb files.',
    minutes: 2,
    requires: [],
    steps: [
      {
        id: 'storage',
        title: 'Versioned notebooks stored beside your runs',
        body:
          'Create one and you get a valid notebook with a version counter that moves every '
          + 'time you save — so "which version made that figure" has an answer, instead of '
          + 'Untitled7 (3).ipynb in your downloads folder.',
        route: '/notebooks',
        anchor: 'notebooks-list',
        side: 'bottom',
      },
      {
        id: 'download',
        title: 'Download works from the card',
        body:
          'The small download icon on each card gives you a real .ipynb without opening it. '
          + 'Since nothing executes here, that round trip — keep it in Prism, run it in '
          + 'Jupyter, paste the result back — is the workflow this page is for.',
        anchor: 'notebooks-list',
        side: 'bottom',
      },
      {
        id: 'no-execution',
        title: 'Running cells needs the kernel built',
        body:
          'The in-browser kernel is generated rather than kept in the repository, and CI builds '
          + 'it into the deployed bundle. Running the dev server straight from a clone will not '
          + 'have it until you run the setup script in public/jupyterlite. Editing is still by '
          + 'raw JSON, and there is no upload — bringing a notebook in is create then paste.',
        anchor: 'notebooks-list',
        side: 'bottom',
      },
    ],
  },
]

/** Every walkthrough, welcome tour first. */
export const allTours: Tour[] = [welcomeTour, ...situations, ...pageTours]

/**
 * Finds a walkthrough by id.
 *
 * @param id The tour identity.
 * @returns The tour, or undefined when the id is unknown — a persisted id can outlive the
 * tour it names, so callers must handle its absence rather than assume.
 */
export function findTour(id: string): Tour | undefined {
  return allTours.find((tour) => tour.id === id)
}
