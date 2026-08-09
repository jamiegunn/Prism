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
          'Prompt Lab keeps a prompt as a template with variables and a history of versions, '
          + 'so "the new wording is better" becomes a claim you can check rather than remember.',
        route: '/prompt-lab',
        anchor: 'nav-prompt-lab',
        side: 'right',
      },
      {
        id: 'ab-test',
        title: 'Run both and compare',
        body:
          'Test two versions against the same set of inputs in one go. Judging a rewrite by '
          + 'trying it once on whatever example is at hand is how prompts get worse while '
          + 'feeling better.',
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
        title: 'Run it again',
        body:
          'Replay puts a past call back into the Playground with its settings intact, which is '
          + 'the quickest way to change one parameter and see what it did.',
        anchor: 'main',
        side: 'bottom',
      },
    ],
  },
]

/** Every walkthrough, welcome tour first. */
export const allTours: Tour[] = [welcomeTour, ...situations]

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
