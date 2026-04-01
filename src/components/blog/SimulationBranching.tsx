import { useState } from "react";

type ConsumerMode = "preview" | "ai" | "commit";

type PipelineStep = {
  name: string;
  detail: string;
};

type Consumer = {
  id: ConsumerMode;
  label: string;
  summary: string;
  resultLabel: string;
  resultValue: string;
  steps: PipelineStep[];
};

const consumers: Consumer[] = [
  {
    id: "preview",
    label: "Player Preview",
    summary:
      "The UI asks the engine to run the real command against a temporary branch, then renders the returned events.",
    resultLabel: "Branch outcome",
    resultValue: "Discard after showing the ghosted result",
    steps: [
      {
        name: "Construct command",
        detail: "Build the same immutable command object the live game would receive.",
      },
      {
        name: "Resolve by identifiers",
        detail: "Handlers resolve the actor and map from keys instead of direct references.",
      },
      {
        name: "Fork touched records",
        detail: "Only the moved actor and the world clock fork into the preview layer.",
      },
      {
        name: "Render events",
        detail: "The UI gets back the exact movement events it needs for a prediction overlay.",
      },
      {
        name: "Drop branch",
        detail: "Because nothing was committed, the temporary snapshot can simply disappear.",
      },
    ],
  },
  {
    id: "ai",
    label: "AI Search",
    summary:
      "The same command path runs inside scoring branches so the AI can compare futures without contaminating live state.",
    resultLabel: "Branch outcome",
    resultValue: "Score and discard unless it wins",
    steps: [
      {
        name: "Construct command",
        detail: "Generate a candidate command from the AI's available actions.",
      },
      {
        name: "Resolve by identifiers",
        detail: "The simulation resolves the same models the player move would touch.",
      },
      {
        name: "Fork touched records",
        detail: "The branch borrows untouched state and only forks the records it mutates.",
      },
      {
        name: "Evaluate result",
        detail: "The AI reads the resulting presentation and assigns a tactical score.",
      },
      {
        name: "Keep only the winner",
        detail: "Most futures are thrown away. Only the best continuation survives to matter.",
      },
    ],
  },
  {
    id: "commit",
    label: "Commit",
    summary:
      "Once a future is accepted, the engine promotes that branch into canonical history instead of inventing a second execution path.",
    resultLabel: "Branch outcome",
    resultValue: "Promote into undoable history",
    steps: [
      {
        name: "Construct command",
        detail: "Use the exact same move command shape the preview already validated.",
      },
      {
        name: "Resolve by identifiers",
        detail: "The engine resolves live models through the same central mutation path.",
      },
      {
        name: "Fork touched records",
        detail: "The write set is still narrow because copy on write works the same way here.",
      },
      {
        name: "Emit events",
        detail: "Views receive the same domain events that preview and AI simulation would have seen.",
      },
      {
        name: "Push to history",
        detail: "The chosen snapshot becomes the new root for undo, redo, and future branches.",
      },
    ],
  },
];

const commandPreview = [
  "new MoveEntityAlongPathToCell(",
  '  ActorId: "Player",',
  '  MapId: "Map1",',
  '  CharacterId: "Player",',
  "  ToX: 5,",
  "  ToY: 10",
  ")",
];

export default function SimulationBranching() {
  const [mode, setMode] = useState<ConsumerMode>("preview");
  const consumer = consumers.find((entry) => entry.id === mode) ?? consumers[0]!;

  return (
    <section className="rounded-[2rem] border border-base-300 bg-base-100 p-5 shadow-sm sm:p-7">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.25em] text-secondary">
          Interactive Flow
        </p>
        <h3 className="m-0 text-2xl font-semibold text-base-content">
          Preview, AI search, and commit all ride the same command path
        </h3>
        <p className="m-0 max-w-2xl text-sm leading-6 text-base-content/70">
          Switch the consumer and watch what changes. The command and mutation
          path stay the same. Only the final handling of the branch differs.
        </p>
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {consumers.map((entry) => (
          <button
            key={entry.id}
            type="button"
            onClick={() => setMode(entry.id)}
            className={entry.id === mode ? "btn btn-secondary btn-sm" : "btn btn-outline btn-sm"}
          >
            {entry.label}
          </button>
        ))}
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[0.88fr_1.12fr]">
        <div className="grid gap-4">
          <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              One Command Object
            </p>
            <div className="mt-3 rounded-2xl bg-base-100 p-4 font-mono text-sm leading-6 text-base-content/80">
              {commandPreview.map((line) => (
                <div key={line}>{line}</div>
              ))}
            </div>
          </div>

          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Consumer Goal
            </p>
            <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
              {consumer.summary}
            </p>
            <div className="mt-4 rounded-2xl bg-base-200/70 p-4">
              <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                {consumer.resultLabel}
              </p>
              <p className="m-0 mt-2 text-sm font-medium text-base-content">
                {consumer.resultValue}
              </p>
            </div>
          </div>
        </div>

        <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
          <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
            Command Lifecycle
          </p>
          <div className="mt-4 grid gap-3">
            {consumer.steps.map((step, index) => (
              <div
                key={`${consumer.id}-${step.name}`}
                className="rounded-2xl border border-base-300 bg-base-100 p-4"
              >
                <div className="flex items-center gap-3">
                  <div className="flex h-8 w-8 items-center justify-center rounded-full bg-secondary/15 text-sm font-semibold text-secondary">
                    {index + 1}
                  </div>
                  <div className="text-base font-semibold text-base-content">
                    {step.name}
                  </div>
                </div>
                <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
                  {step.detail}
                </p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
