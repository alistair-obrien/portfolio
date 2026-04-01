import { useState } from "react";

type RecordState = "forked" | "shared";

type RecordSnapshot = {
  name: string;
  value: string;
  state: RecordState;
};

type Scenario = {
  id: string;
  label: string;
  detail: string;
  why: string;
  recordsWritten: number;
  records: RecordSnapshot[];
};

const baseRecordCount = 6;

const scenarios: Scenario[] = [
  {
    id: "move",
    label: "Move one actor",
    detail: "A normal move updates the world clock and the actor position.",
    why: "Only the records that actually changed need to fork into the branch.",
    recordsWritten: 2,
    records: [
      { name: "World", value: "turn 18 | alert 1", state: "forked" },
      { name: "Hero", value: "tile C3 | 2 AP", state: "forked" },
      { name: "Sentinel", value: "tile E4 | patrol", state: "shared" },
      { name: "Door", value: "closed", state: "shared" },
      { name: "ThreatMap", value: "stable", state: "shared" },
      { name: "Loot", value: "unclaimed", state: "shared" },
    ],
  },
  {
    id: "door",
    label: "Preview opening a door",
    detail: "The preview branch opens the door and advances the local clock.",
    why: "The preview can be rendered and discarded without cloning the untouched world.",
    recordsWritten: 2,
    records: [
      { name: "World", value: "turn 19 | alert 2", state: "forked" },
      { name: "Hero", value: "tile C3 | 2 AP", state: "shared" },
      { name: "Sentinel", value: "tile E4 | patrol", state: "shared" },
      { name: "Door", value: "open", state: "forked" },
      { name: "ThreatMap", value: "stable", state: "shared" },
      { name: "Loot", value: "unclaimed", state: "shared" },
    ],
  },
  {
    id: "response",
    label: "Simulate the guard response",
    detail: "An AI branch advances the enemy and updates the threat model.",
    why: "Branching stays cheap because unchanged data keeps borrowing the older snapshot.",
    recordsWritten: 3,
    records: [
      { name: "World", value: "turn 20 | alert 3", state: "forked" },
      { name: "Hero", value: "tile C3 | 2 AP", state: "shared" },
      { name: "Sentinel", value: "tile D4 | investigate", state: "forked" },
      { name: "Door", value: "open", state: "shared" },
      { name: "ThreatMap", value: "hero exposed on south lane", state: "forked" },
      { name: "Loot", value: "unclaimed", state: "shared" },
    ],
  },
];

function recordStyle(state: RecordState) {
  return state === "forked"
    ? "border-primary/40 bg-primary/10 text-base-content"
    : "border-base-300 bg-base-100 text-base-content/55";
}

function badgeStyle(state: RecordState) {
  return state === "forked" ? "badge badge-primary badge-sm" : "badge badge-ghost badge-sm";
}

export default function CopyOnWriteHistory() {
  const [scenarioId, setScenarioId] = useState(scenarios[0]!.id);
  const scenario = scenarios.find((entry) => entry.id === scenarioId) ?? scenarios[0]!;
  const savedWrites = baseRecordCount - scenario.recordsWritten;

  return (
    <section className="rounded-[2rem] border border-base-300 bg-base-100 p-5 shadow-sm sm:p-7">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.25em] text-primary">
          Interactive Delta
        </p>
        <h3 className="m-0 text-2xl font-semibold text-base-content">
          Copy on write keeps each branch as small as the change
        </h3>
        <p className="m-0 max-w-2xl text-sm leading-6 text-base-content/70">
          Pick a command and inspect the branch it produces. Highlighted records
          are the only ones that fork into the new snapshot.
        </p>
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {scenarios.map((entry) => {
          const active = entry.id === scenario.id;

          return (
            <button
              key={entry.id}
              type="button"
              onClick={() => setScenarioId(entry.id)}
              className={active ? "btn btn-primary btn-sm" : "btn btn-outline btn-sm"}
            >
              {entry.label}
            </button>
          );
        })}
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[1.15fr_0.85fr]">
        <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                Branch Snapshot
              </p>
              <h4 className="m-0 mt-1 text-xl font-semibold text-base-content">
                {scenario.label}
              </h4>
            </div>
            <span className="badge badge-primary badge-lg">
              {scenario.recordsWritten} records forked
            </span>
          </div>

          <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
            {scenario.detail}
          </p>

          <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {scenario.records.map((record) => (
              <div
                key={record.name}
                className={`rounded-2xl border p-4 transition ${recordStyle(record.state)}`}
              >
                <div className="flex items-center justify-between gap-2">
                  <div className="text-sm font-semibold">{record.name}</div>
                  <span className={badgeStyle(record.state)}>{record.state}</span>
                </div>
                <p className="m-0 mt-2 text-sm leading-6">{record.value}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="grid gap-4">
          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Write Budget
            </p>
            <div className="mt-4 grid gap-3 sm:grid-cols-3 lg:grid-cols-1">
              <div>
                <div className="text-3xl font-semibold text-base-content">
                  {baseRecordCount}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  full clone writes
                </p>
              </div>
              <div>
                <div className="text-3xl font-semibold text-primary">
                  {scenario.recordsWritten}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  branch writes
                </p>
              </div>
              <div>
                <div className="text-3xl font-semibold text-success">
                  {savedWrites}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  records still shared
                </p>
              </div>
            </div>
          </div>

          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Why This Matters
            </p>
            <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
              {scenario.why}
            </p>
          </div>

          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Mental Model
            </p>
            <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
              The branch is not a cloned world. It is just a thin layer of
              replacements sitting on top of older shared records.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
