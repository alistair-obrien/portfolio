import { useState } from "react";

type RecordCell = {
  name: string;
  value: string;
  state: "new" | "forked" | "shared";
};

type Snapshot = {
  id: string;
  action: string;
  summary: string;
  writtenRecords: number;
  records: RecordCell[];
};

const snapshots: Snapshot[] = [
  {
    id: "T0",
    action: "Seed the room",
    summary: "The first snapshot owns every record because nothing exists yet.",
    writtenRecords: 5,
    records: [
      { name: "World", value: "turn 0 | alert 1", state: "new" },
      { name: "Hero", value: "tile A2 | 3 AP", state: "new" },
      { name: "Sentinel", value: "tile D3 | patrol", state: "new" },
      { name: "Door", value: "closed", state: "new" },
      { name: "Loot", value: "still on pedestal", state: "new" },
    ],
  },
  {
    id: "T1",
    action: "Hero steps forward",
    summary: "Only the world clock and the hero record fork. Everything else is reused.",
    writtenRecords: 2,
    records: [
      { name: "World", value: "turn 1 | alert 1", state: "forked" },
      { name: "Hero", value: "tile B2 | 2 AP", state: "forked" },
      { name: "Sentinel", value: "tile D3 | patrol", state: "shared" },
      { name: "Door", value: "closed", state: "shared" },
      { name: "Loot", value: "still on pedestal", state: "shared" },
    ],
  },
  {
    id: "T2",
    action: "Player previews opening the door",
    summary: "A preview snapshot forks only what the interaction changes, so it is cheap to create and cheap to throw away.",
    writtenRecords: 2,
    records: [
      { name: "World", value: "turn 2 | alert 2", state: "forked" },
      { name: "Hero", value: "tile B2 | 1 AP", state: "shared" },
      { name: "Sentinel", value: "tile D3 | patrol", state: "shared" },
      { name: "Door", value: "open", state: "forked" },
      { name: "Loot", value: "still on pedestal", state: "shared" },
    ],
  },
  {
    id: "T3",
    action: "AI simulates the guard response",
    summary: "The branch keeps sharing unchanged data while forking only the records touched by the reaction simulation.",
    writtenRecords: 2,
    records: [
      { name: "World", value: "turn 3 | alert 3", state: "forked" },
      { name: "Hero", value: "tile B2 | 1 AP", state: "shared" },
      { name: "Sentinel", value: "tile C3 | investigate", state: "forked" },
      { name: "Door", value: "open", state: "shared" },
      { name: "Loot", value: "still on pedestal", state: "shared" },
    ],
  },
  {
    id: "T4",
    action: "Commit the better outcome",
    summary: "Accepting a branch is just choosing a different snapshot root for future history.",
    writtenRecords: 3,
    records: [
      { name: "World", value: "turn 4 | alert 2", state: "forked" },
      { name: "Hero", value: "tile C2 | 0 AP", state: "forked" },
      { name: "Sentinel", value: "tile C3 | investigate", state: "shared" },
      { name: "Door", value: "open", state: "shared" },
      { name: "Loot", value: "picked up", state: "forked" },
    ],
  },
];

const totalRecordsPerSnapshot = snapshots[0].records.length;

function badgeClass(state: RecordCell["state"]) {
  if (state === "new") {
    return "badge badge-accent badge-outline";
  }

  if (state === "forked") {
    return "badge badge-primary";
  }

  return "badge badge-ghost";
}

export default function CopyOnWriteHistory() {
  const [index, setIndex] = useState(2);
  const snapshot = snapshots[index];

  const cloneWrites = totalRecordsPerSnapshot * (index + 1);
  const cowWrites = snapshots
    .slice(0, index + 1)
    .reduce((total, step) => total + step.writtenRecords, 0);
  const savedWrites = cloneWrites - cowWrites;

  return (
    <section className="rounded-[2rem] border border-base-300 bg-base-100 p-5 shadow-sm sm:p-7">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-[0.25em] text-primary">
            Interactive History
          </p>
          <h3 className="m-0 text-2xl font-semibold text-base-content">
            Scrub snapshots without cloning the whole world
          </h3>
          <p className="m-0 max-w-2xl text-sm leading-6 text-base-content/70">
            Move the cursor through the timeline. The highlighted records show
            what that snapshot actually owns, while the ghosted ones are still
            shared with earlier history.
          </p>
        </div>

        <div className="flex gap-2">
          <button
            type="button"
            className="btn btn-sm btn-outline"
            disabled={index === 0}
            onClick={() => setIndex((value) => Math.max(0, value - 1))}
          >
            Previous
          </button>
          <button
            type="button"
            className="btn btn-sm btn-primary"
            disabled={index === snapshots.length - 1}
            onClick={() =>
              setIndex((value) => Math.min(snapshots.length - 1, value + 1))
            }
          >
            Next
          </button>
        </div>
      </div>

      <div className="mt-6 space-y-4">
        <input
          type="range"
          min={0}
          max={snapshots.length - 1}
          value={index}
          onChange={(event) => setIndex(Number(event.target.value))}
          className="range range-primary"
          aria-label="Snapshot timeline"
        />

        <div className="grid gap-2 sm:grid-cols-5">
          {snapshots.map((step, stepIndex) => {
            const active = stepIndex === index;

            return (
              <button
                key={step.id}
                type="button"
                onClick={() => setIndex(stepIndex)}
                className={[
                  "rounded-2xl border px-3 py-3 text-left transition",
                  active
                    ? "border-primary bg-primary/10 shadow-sm"
                    : "border-base-300 bg-base-200/60 hover:border-primary/40",
                ].join(" ")}
              >
                <div className="text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                  {step.id}
                </div>
                <div className="mt-1 text-sm font-medium text-base-content">
                  {step.action}
                </div>
              </button>
            );
          })}
        </div>
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[1.15fr_0.85fr]">
        <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                Snapshot {snapshot.id}
              </p>
              <h4 className="m-0 mt-1 text-xl font-semibold text-base-content">
                {snapshot.action}
              </h4>
            </div>
            <span className="badge badge-primary badge-lg">
              {snapshot.writtenRecords} records written
            </span>
          </div>

          <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
            {snapshot.summary}
          </p>

          <div className="mt-5 overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Record</th>
                  <th>State</th>
                  <th>Value at this snapshot</th>
                </tr>
              </thead>
              <tbody>
                {snapshot.records.map((record) => (
                  <tr key={record.name}>
                    <td className="font-medium">{record.name}</td>
                    <td>
                      <span className={badgeClass(record.state)}>
                        {record.state}
                      </span>
                    </td>
                    <td className="text-base-content/70">{record.value}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="grid gap-4">
          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Allocation Check
            </p>
            <div className="mt-3 grid gap-3 sm:grid-cols-3 lg:grid-cols-1">
              <div>
                <div className="text-3xl font-semibold text-base-content">
                  {cloneWrites}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  writes with full cloning
                </p>
              </div>
              <div>
                <div className="text-3xl font-semibold text-primary">
                  {cowWrites}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  writes with copy on write
                </p>
              </div>
              <div>
                <div className="text-3xl font-semibold text-success">
                  {savedWrites}
                </div>
                <p className="m-0 text-sm text-base-content/60">
                  records avoided so far
                </p>
              </div>
            </div>
          </div>

          <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Why It Matters
            </p>
            <p className="m-0 mt-3 text-sm leading-6 text-base-content/70">
              Traversing history stops being a replay problem. Each point in
              time already has a stable root snapshot, so rewinding is mostly
              pointer swapping plus whatever view refresh the editor needs.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
