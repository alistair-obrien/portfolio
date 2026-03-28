import { useState } from "react";

type Mode = "preview" | "ai" | "commit";

type Branch = {
  id: string;
  label: string;
  score: number;
  cost: string;
  touched: string[];
  result: string;
  steps: {
    tick: string;
    text: string;
    writes: string[];
  }[];
  explanations: Record<Mode, string>;
};

const branches: Branch[] = [
  {
    id: "dash",
    label: "Dash behind the pillar",
    score: 92,
    cost: "2 AP",
    touched: ["World", "Hero", "ThreatMap"],
    result: "Breaks line of fire and preserves tempo for the next turn.",
    steps: [
      {
        tick: "+1",
        text: "Hero dashes from B2 to C1.",
        writes: ["World", "Hero"],
      },
      {
        tick: "+2",
        text: "Threat map recomputes and the turret loses sight.",
        writes: ["ThreatMap"],
      },
      {
        tick: "+3",
        text: "Damage forecast drops from 3 to 0.",
        writes: [],
      },
    ],
    explanations: {
      preview:
        "For the player, this branch powers a ghosted destination, damage preview, and confidence that the move is legal before commit.",
      ai: "For AI search, this branch gets scored without mutating the live match. If a better line appears, this one is discarded with almost no cleanup.",
      commit:
        "When the move is accepted, the engine promotes this branch root into main history instead of replaying the whole plan.",
    },
  },
  {
    id: "hack",
    label: "Hack the nearby console",
    score: 61,
    cost: "1 AP",
    touched: ["World", "Hero", "Console", "Door"],
    result: "Opens a shortcut, but leaves the hero exposed during the enemy turn.",
    steps: [
      {
        tick: "+1",
        text: "Hero interacts with the console.",
        writes: ["World", "Hero", "Console"],
      },
      {
        tick: "+2",
        text: "The side door unlocks.",
        writes: ["Door"],
      },
      {
        tick: "+3",
        text: "Sentinel keeps line of sight and predicted damage stays high.",
        writes: [],
      },
    ],
    explanations: {
      preview:
        "This is still a useful preview because the player can see the shortcut open before deciding whether the positional risk is worth it.",
      ai: "The AI can keep this candidate around as a tactical option, but the lower score tells it to prefer safer routes first.",
      commit:
        "If the player wants the shortcut anyway, the branch is already ready to become the new canonical future.",
    },
  },
  {
    id: "wait",
    label: "Hold position and overwatch",
    score: 47,
    cost: "0 AP",
    touched: ["World", "Hero", "OverwatchState"],
    result: "Cheap to evaluate, but it gives initiative back to the enemy.",
    steps: [
      {
        tick: "+1",
        text: "Hero enters overwatch.",
        writes: ["World", "Hero", "OverwatchState"],
      },
      {
        tick: "+2",
        text: "The sentinel advances one tile.",
        writes: [],
      },
      {
        tick: "+3",
        text: "The branch remains valid, but the position worsens.",
        writes: [],
      },
    ],
    explanations: {
      preview:
        "Even passive actions benefit from previews because you can forecast whether waiting actually changes anything meaningful.",
      ai: "Search systems love cheap branches like this because they are fast to simulate and easy to prune when a stronger line exists.",
      commit:
        "If no better option exists, committing this branch still costs only the writes it touched, not a full world duplication.",
    },
  },
];

const modes: { id: Mode; label: string }[] = [
  { id: "preview", label: "Player Preview" },
  { id: "ai", label: "AI Search" },
  { id: "commit", label: "Commit" },
];

const baselineRecords = 8;

export default function SimulationBranching() {
  const [mode, setMode] = useState<Mode>("preview");
  const [selectedBranchId, setSelectedBranchId] = useState(branches[0].id);
  const [committedBranchId, setCommittedBranchId] = useState<string | null>(
    null,
  );

  const selectedBranch =
    branches.find((branch) => branch.id === selectedBranchId) ?? branches[0];

  const sharedCount = baselineRecords - selectedBranch.touched.length;

  return (
    <section className="rounded-[2rem] border border-base-300 bg-base-100 p-5 shadow-sm sm:p-7">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.25em] text-secondary">
          Interactive Branching
        </p>
        <h3 className="m-0 text-2xl font-semibold text-base-content">
          Previews and AI pathing are the same operation
        </h3>
        <p className="m-0 max-w-2xl text-sm leading-6 text-base-content/70">
          Pick a consumer above the simulation, then inspect a branch. The live
          state stays untouched while the branch borrows unchanged records from
          its parent snapshot.
        </p>
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {modes.map((entry) => {
          const active = entry.id === mode;

          return (
            <button
              key={entry.id}
              type="button"
              className={active ? "btn btn-primary btn-sm" : "btn btn-outline btn-sm"}
              onClick={() => setMode(entry.id)}
            >
              {entry.label}
            </button>
          );
        })}
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[0.95fr_1.05fr]">
        <div className="space-y-3">
          {branches.map((branch) => {
            const active = branch.id === selectedBranch.id;

            return (
              <button
                key={branch.id}
                type="button"
                onClick={() => setSelectedBranchId(branch.id)}
                className={[
                  "w-full rounded-3xl border p-4 text-left transition",
                  active
                    ? "border-secondary bg-secondary/10 shadow-sm"
                    : "border-base-300 bg-base-200/50 hover:border-secondary/40",
                ].join(" ")}
              >
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <div className="text-lg font-semibold text-base-content">
                      {branch.label}
                    </div>
                    <div className="mt-1 text-sm text-base-content/60">
                      {branch.cost} to explore
                    </div>
                  </div>
                  <div className="badge badge-secondary badge-lg">
                    score {branch.score}
                  </div>
                </div>
                <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
                  {branch.result}
                </p>
              </button>
            );
          })}
        </div>

        <div className="grid gap-4">
          <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                  Selected Branch
                </p>
                <h4 className="m-0 mt-1 text-xl font-semibold text-base-content">
                  {selectedBranch.label}
                </h4>
              </div>
              <div className="flex gap-2">
                <span className="badge badge-outline">
                  {selectedBranch.touched.length} forked
                </span>
                <span className="badge badge-ghost">{sharedCount} shared</span>
              </div>
            </div>

            <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
              {selectedBranch.explanations[mode]}
            </p>

            <div className="mt-5 grid gap-3">
              {selectedBranch.steps.map((step) => (
                <div
                  key={`${selectedBranch.id}-${step.tick}`}
                  className="rounded-2xl border border-base-300 bg-base-100 p-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="badge badge-primary">{step.tick}</span>
                    <span className="text-xs uppercase tracking-[0.2em] text-base-content/45">
                      writes: {step.writes.length > 0 ? step.writes.join(", ") : "none"}
                    </span>
                  </div>
                  <p className="m-0 mt-3 text-sm leading-6 text-base-content/75">
                    {step.text}
                  </p>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                  Branch Decision
                </p>
                <p className="m-0 mt-2 text-sm leading-6 text-base-content/70">
                  Main history is still parked at the pre-branch snapshot until
                  you explicitly accept a future.
                </p>
              </div>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => setCommittedBranchId(selectedBranch.id)}
              >
                Promote branch
              </button>
            </div>

            <div className="mt-4 rounded-2xl bg-base-200/70 p-4 text-sm leading-6 text-base-content/75">
              {committedBranchId === selectedBranch.id
                ? `Committed: ${selectedBranch.label} is now the chosen continuation of history.`
                : committedBranchId
                  ? `Previously committed: ${
                      branches.find((branch) => branch.id === committedBranchId)
                        ?.label
                    }. You can still inspect alternatives without mutating the live root.`
                  : "No branch has been committed yet. You are only exploring possible futures."}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
