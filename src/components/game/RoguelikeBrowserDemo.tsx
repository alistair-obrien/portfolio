import { startTransition, useEffect, useRef, useState } from "react";

type IdLike = { Value?: string | null } | string | null | undefined;

type EngineEvent = {
  Type?: string;
  Description?: string;
};

type PlayerHud = {
  characterUid?: IdLike;
};

type CharacterPlacement = {
  CharacterId?: IdLike;
  CellFootprint?: string | null;
};

type TilePresentation = {
  CellFootprint?: string | null;
  Walkable?: boolean;
};

type ItemPlacement = {
  CellFootprint?: string | null;
};

type PropPlacement = {
  CellFootprint?: string | null;
};

type MapPresentation = {
  MapId?: IdLike;
  Width?: number;
  Height?: number;
  Tiles?: TilePresentation[] | null;
  Characters?: CharacterPlacement[] | null;
  Items?: ItemPlacement[] | null;
  Props?: PropPlacement[] | null;
};

type GameState = {
  Maps?: MapPresentation[] | null;
  PlayerHUD?: PlayerHud | null;
};

type EngineEnvelope = {
  Ok?: boolean;
  SessionId?: string;
  ErrorMessage?: string | null;
  Events?: EngineEvent[] | null;
  State?: GameState | null;
};

type RoguelikeEngineModule = {
  createSession(): Promise<EngineEnvelope>;
  resetSession(sessionId: string): Promise<EngineEnvelope>;
  movePlayerToCell(
    sessionId: string,
    mapId: string,
    x: number,
    y: number,
  ): Promise<EngineEnvelope>;
  disposeSession(sessionId: string): Promise<boolean>;
};

type LogEntry = {
  command: string;
  ok: boolean;
  details: string[];
};

type Footprint = {
  x: number;
  y: number;
  width: number;
  height: number;
};

function getIdValue(id: IdLike): string | null {
  if (!id) {
    return null;
  }

  if (typeof id === "string") {
    return id;
  }

  return id.Value ?? null;
}

function parseFootprint(value: string | null | undefined): Footprint | null {
  if (!value) {
    return null;
  }

  const [x, y, width, height] = value.split(":").map(Number);

  if ([x, y, width, height].some((part) => Number.isNaN(part))) {
    return null;
  }

  return { x, y, width, height };
}

function footprintContains(
  footprint: string | null | undefined,
  x: number,
  y: number,
) {
  const parsed = parseFootprint(footprint);

  if (!parsed) {
    return false;
  }

  return (
    x >= parsed.x &&
    x < parsed.x + parsed.width &&
    y >= parsed.y &&
    y < parsed.y + parsed.height
  );
}

function getTileAt(map: MapPresentation, x: number, y: number) {
  return (map.Tiles ?? []).find((tile) => footprintContains(tile.CellFootprint, x, y));
}

function getActiveMap(state: GameState | null) {
  const maps = state?.Maps ?? [];
  const playerId = getIdValue(state?.PlayerHUD?.characterUid);

  if (playerId) {
    const playerMap = maps.find((map) =>
      (map.Characters ?? []).some(
        (character) => getIdValue(character.CharacterId) === playerId,
      ),
    );

    if (playerMap) {
      return playerMap;
    }
  }

  return maps[0] ?? null;
}

function buildLogEntry(command: string, envelope: EngineEnvelope): LogEntry {
  const ok = envelope.Ok === true;

  if (!ok) {
    return {
      command,
      ok: false,
      details: [envelope.ErrorMessage || "Command failed."],
    };
  }

  const details =
    envelope.Events?.length && envelope.Events.length > 0
      ? envelope.Events.map((event) => event.Type || event.Description || "Event")
      : ["OK"];

  return { command, ok: true, details };
}

function prependLogEntry(entries: LogEntry[], entry: LogEntry) {
  return [entry, ...entries].slice(0, 12);
}

async function loadEngineModule() {
  const moduleUrl = "/widgets/roguelike/engine-loader.js";
  const runtimeImport = new Function(
    "path",
    "return import(path);",
  ) as (path: string) => Promise<unknown>;

  return (await runtimeImport(moduleUrl)) as RoguelikeEngineModule;
}

export default function RoguelikeBrowserDemo() {
  const engineRef = useRef<RoguelikeEngineModule | null>(null);
  const sessionIdRef = useRef<string | null>(null);

  const [state, setState] = useState<GameState | null>(null);
  const [logEntries, setLogEntries] = useState<LogEntry[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    let disposed = false;

    async function boot() {
      try {
        const engine = await loadEngineModule();
        const envelope = await engine.createSession();

        if (disposed) {
          if (envelope.SessionId) {
            await engine.disposeSession(envelope.SessionId);
          }

          return;
        }

        engineRef.current = engine;
        sessionIdRef.current = envelope.SessionId ?? null;

        if (!envelope.Ok || !envelope.State || !envelope.SessionId) {
          setErrorMessage(envelope.ErrorMessage || "Failed to create a browser-local session.");
          return;
        }

        startTransition(() => {
          setState(envelope.State ?? null);
          setErrorMessage(null);
          setLogEntries([
            {
              command: "reset",
              ok: true,
              details: ["Seeded development world."],
            },
          ]);
        });
      } catch (error) {
        if (!disposed) {
          setErrorMessage(
            error instanceof Error ? error.message : "Failed to boot the browser-local engine.",
          );
        }
      }
    }

    void boot();

    return () => {
      disposed = true;

      const engine = engineRef.current;
      const sessionId = sessionIdRef.current;

      if (engine && sessionId) {
        void engine.disposeSession(sessionId);
      }
    };
  }, []);

  const activeMap = getActiveMap(state);
  const playerId = getIdValue(state?.PlayerHUD?.characterUid);

  async function handleReset() {
    const engine = engineRef.current;
    const sessionId = sessionIdRef.current;

    if (!engine || !sessionId || isBusy) {
      return;
    }

    setIsBusy(true);

    try {
      const envelope = await engine.resetSession(sessionId);

      startTransition(() => {
        setErrorMessage(envelope.Ok ? null : envelope.ErrorMessage || "Reset failed.");

        if (envelope.State) {
          setState(envelope.State);
        }

        setLogEntries((current) =>
          prependLogEntry(
            current,
            envelope.Ok
              ? { command: "reset", ok: true, details: ["Seeded development world."] }
              : buildLogEntry("reset", envelope),
          ),
        );
      });
    } finally {
      setIsBusy(false);
    }
  }

  async function handleMove(mapId: string, x: number, y: number) {
    const engine = engineRef.current;
    const sessionId = sessionIdRef.current;

    if (!engine || !sessionId || isBusy) {
      return;
    }

    setIsBusy(true);

    try {
      const envelope = await engine.movePlayerToCell(sessionId, mapId, x, y);

      startTransition(() => {
        setErrorMessage(envelope.Ok ? null : envelope.ErrorMessage || "Move failed.");

        if (envelope.State) {
          setState(envelope.State);
        }

        setLogEntries((current) =>
          prependLogEntry(current, buildLogEntry(`move (${x}, ${y})`, envelope)),
        );
      });
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button
          type="button"
          onClick={handleReset}
          disabled={!sessionIdRef.current || isBusy}
          className="rounded-full border border-base-300 bg-base-100 px-4 py-2 text-sm font-medium text-base-content transition hover:bg-base-200 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Reset
        </button>
      </div>

      <div className="overflow-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-3 shadow-sm">
        {activeMap ? (
          <div
            className="grid w-max gap-px rounded-xl bg-base-300 p-px"
            style={{
              gridTemplateColumns: `repeat(${activeMap.Width ?? 0}, minmax(0, 1fr))`,
            }}
          >
            {Array.from({ length: activeMap.Height ?? 0 }, (_, y) =>
              Array.from({ length: activeMap.Width ?? 0 }, (_, x) => {
                const mapId = getIdValue(activeMap.MapId) ?? "";
                const tile = getTileAt(activeMap, x, y);
                const isWalkable = tile?.Walkable !== false;
                const playerAtCell = (activeMap.Characters ?? []).some(
                  (character) =>
                    getIdValue(character.CharacterId) === playerId &&
                    footprintContains(character.CellFootprint, x, y),
                );
                const characterAtCell = (activeMap.Characters ?? []).some(
                  (character) =>
                    getIdValue(character.CharacterId) !== playerId &&
                    footprintContains(character.CellFootprint, x, y),
                );
                const itemAtCell = (activeMap.Items ?? []).some((item) =>
                  footprintContains(item.CellFootprint, x, y),
                );
                const propAtCell = (activeMap.Props ?? []).some((prop) =>
                  footprintContains(prop.CellFootprint, x, y),
                );
                const canAttemptMove =
                  isWalkable && !propAtCell && !characterAtCell && !playerAtCell;

                let label = "";
                let tileClassName =
                  "bg-base-200 text-base-content/20 hover:bg-base-200 hover:text-base-content/40";

                if (!isWalkable) {
                  label = "#";
                  tileClassName =
                    "bg-base-300 text-base-content/55 hover:bg-base-300 hover:text-base-content/55";
                } else if (propAtCell) {
                  label = "#";
                  tileClassName = "bg-base-300 text-base-content/70 hover:bg-base-300";
                } else if (playerAtCell) {
                  label = "@";
                  tileClassName = "bg-neutral text-neutral-content hover:bg-neutral";
                } else if (characterAtCell) {
                  label = "n";
                  tileClassName = "bg-secondary/20 text-secondary hover:bg-secondary/25";
                } else if (itemAtCell) {
                  label = "*";
                  tileClassName = "bg-accent/20 text-accent hover:bg-accent/25";
                } else {
                  label = ".";
                }

                return (
                  <button
                    key={`${x}:${y}`}
                    type="button"
                    disabled={isBusy || !canAttemptMove}
                    onClick={() => void handleMove(mapId, x, y)}
                    className={`flex h-4 w-4 items-center justify-center text-[9px] leading-none transition ${tileClassName} disabled:cursor-default disabled:opacity-100`}
                    aria-label={`Move player to ${x}, ${y}`}
                  >
                    {label}
                  </button>
                );
              }),
            )}
          </div>
        ) : (
          <div className="min-h-[32rem] rounded-xl border border-dashed border-base-300 bg-base-100" />
        )}
      </div>

      <div className="rounded-[1.5rem] border border-base-300 bg-base-100 p-3 shadow-sm">
        <div className="max-h-56 overflow-auto rounded-xl bg-base-200 p-3 font-mono text-xs leading-6 text-base-content">
          {errorMessage ? (
            <div className="text-error">{errorMessage}</div>
          ) : logEntries.length > 0 ? (
            logEntries.map((entry, index) => (
              <div key={`${entry.command}-${index}`}>
                <span className={entry.ok ? "text-success" : "text-error"}>
                  [{entry.ok ? "OK" : "ERR"}]
                </span>{" "}
                {entry.command}
                {entry.details.length > 0 ? ` :: ${entry.details.join(", ")}` : ""}
              </div>
            ))
          ) : null}
        </div>
      </div>
    </div>
  );
}
