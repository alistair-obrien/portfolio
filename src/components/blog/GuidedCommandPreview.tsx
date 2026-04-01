import { startTransition, useEffect, useMemo, useRef, useState } from "react";

type IdLike = { Value?: string | null } | string | null | undefined;

type CommandResult = {
  Ok?: boolean;
  ErrorMessage?: string | null;
};

type EngineEventPayload = {
  MoveResult?: {
    From?: string | null;
    To?: string | null;
  } | null;
};

type EngineEvent = {
  Type?: string;
  Description?: string;
  Payload?: EngineEventPayload | null;
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
  SessionId?: string;
  Result?: CommandResult | null;
  Events?: EngineEvent[] | null;
  State?: GameState | null;
};

type EngineCommand = {
  Type: string;
  Payload: Record<string, unknown>;
};

type EnginePathStep = {
  FromX?: number;
  FromY?: number;
  FromWidth?: number;
  FromHeight?: number;
  ToX?: number;
  ToY?: number;
  ToWidth?: number;
  ToHeight?: number;
};

type RoguelikeEngineModule = {
  createCommand(type: string, payload: Record<string, unknown>): EngineCommand;
  createSession(): Promise<EngineEnvelope>;
  resetSession(sessionId: string): Promise<EngineEnvelope>;
  disposeSession(sessionId: string): Promise<EngineEnvelope>;
  getGameState(sessionId: string): Promise<EngineEnvelope>;
  executeTrackedCommand(sessionId: string, command: EngineCommand): Promise<EngineEnvelope>;
  executePreviewCommand(sessionId: string, command: EngineCommand): Promise<EngineEnvelope>;
};

type Footprint = {
  x: number;
  y: number;
  width: number;
  height: number;
};

type PreparedMap = {
  mapId: string;
  width: number;
  height: number;
  walkableCells: Set<string>;
  propCells: Set<string>;
  itemCells: Set<string>;
  playerCells: Set<string>;
  characterCells: Set<string>;
  playerFootprint: Footprint | null;
};

type MoveOption = {
  id: string;
  label: string;
  detail: string;
  x: number;
  y: number;
  direction: string;
  path: Array<Required<EnginePathStep>>;
};

type StatusCard = {
  title: string;
  detail: string;
  tone: "muted" | "success";
  events: string[];
};

const MOVE_COMMAND_TYPE = "MapsAPI.Commands.MoveCharacterAlongPathToCell";

function getIdValue(id: IdLike): string | null {
  if (!id) {
    return null;
  }

  if (typeof id === "string") {
    return normalizeTypedIdValue(id);
  }

  return normalizeTypedIdValue(id.Value ?? null);
}

function normalizeTypedIdValue(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  const separatorIndex = value.indexOf(":");

  if (separatorIndex > 0 && separatorIndex < value.length - 1) {
    return value.slice(separatorIndex + 1);
  }

  return value;
}

function createTypedId(typeName: string, value: string | null) {
  return value ? `${typeName}:${value}` : null;
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

function toCellKey(x: number, y: number) {
  return `${x}:${y}`;
}

function addFootprintToSet(
  target: Set<string>,
  footprintValue: string | null | undefined,
) {
  const footprint = parseFootprint(footprintValue);

  if (!footprint) {
    return;
  }

  for (let yy = footprint.y; yy < footprint.y + footprint.height; yy += 1) {
    for (let xx = footprint.x; xx < footprint.x + footprint.width; xx += 1) {
      target.add(toCellKey(xx, yy));
    }
  }
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

function prepareMap(
  map: MapPresentation | null,
  playerId: string | null,
): PreparedMap | null {
  if (!map) {
    return null;
  }

  const prepared: PreparedMap = {
    mapId: getIdValue(map.MapId) ?? "",
    width: map.Width ?? 0,
    height: map.Height ?? 0,
    walkableCells: new Set<string>(),
    propCells: new Set<string>(),
    itemCells: new Set<string>(),
    playerCells: new Set<string>(),
    characterCells: new Set<string>(),
    playerFootprint: null,
  };

  for (const tile of map.Tiles ?? []) {
    const footprint = parseFootprint(tile.CellFootprint);

    if (!footprint) {
      continue;
    }

    for (let yy = footprint.y; yy < footprint.y + footprint.height; yy += 1) {
      for (let xx = footprint.x; xx < footprint.x + footprint.width; xx += 1) {
        const key = toCellKey(xx, yy);

        if (tile.Walkable !== false) {
          prepared.walkableCells.add(key);
        }
      }
    }
  }

  for (const prop of map.Props ?? []) {
    addFootprintToSet(prepared.propCells, prop.CellFootprint);
  }

  for (const item of map.Items ?? []) {
    addFootprintToSet(prepared.itemCells, item.CellFootprint);
  }

  for (const character of map.Characters ?? []) {
    const isPlayer = getIdValue(character.CharacterId) === playerId;
    const footprint = parseFootprint(character.CellFootprint);

    if (isPlayer && footprint) {
      prepared.playerFootprint = footprint;
    }

    addFootprintToSet(
      isPlayer ? prepared.playerCells : prepared.characterCells,
      character.CellFootprint,
    );
  }

  return prepared;
}

function normalizePath(path: EnginePathStep[] | null | undefined) {
  return (path ?? []).filter(
    (step) =>
      typeof step.FromX === "number" &&
      typeof step.FromY === "number" &&
      typeof step.FromWidth === "number" &&
      typeof step.FromHeight === "number" &&
      typeof step.ToX === "number" &&
      typeof step.ToY === "number" &&
      typeof step.ToWidth === "number" &&
      typeof step.ToHeight === "number",
  ) as Array<Required<EnginePathStep>>;
}

function normalizePathFromEvents(events: EngineEvent[] | null | undefined) {
  return normalizePath(
    (events ?? []).flatMap((event) => {
      const moveResult = event.Payload?.MoveResult;
      const from = parseFootprint(moveResult?.From);
      const to = parseFootprint(moveResult?.To);

      if (!from || !to) {
        return [];
      }

      return [
        {
          FromX: from.x,
          FromY: from.y,
          FromWidth: from.width,
          FromHeight: from.height,
          ToX: to.x,
          ToY: to.y,
          ToWidth: to.width,
          ToHeight: to.height,
        },
      ];
    }),
  );
}

function isEnvelopeOk(envelope: EngineEnvelope) {
  return envelope.Result?.Ok === true;
}

function getEnvelopeError(envelope: EngineEnvelope, fallback: string) {
  return envelope.Result?.ErrorMessage || fallback;
}

function createMovePlayerCommand(
  engine: RoguelikeEngineModule,
  mapId: string,
  playerId: string,
  x: number,
  y: number,
) {
  return engine.createCommand(MOVE_COMMAND_TYPE, {
    ActorId: createTypedId("CharacterId", playerId),
    MapId: createTypedId("MapChunkId", mapId),
    CharacterId: createTypedId("CharacterId", playerId),
    ToX: x,
    ToY: y,
  });
}

async function loadEngineModule() {
  const moduleUrl = "/widgets/roguelike/engine-loader.js";
  const runtimeImport = new Function(
    "path",
    "return import(path);",
  ) as (path: string) => Promise<unknown>;

  return (await runtimeImport(moduleUrl)) as RoguelikeEngineModule;
}

function classifyDirection(dx: number, dy: number) {
  if (Math.abs(dx) >= Math.abs(dy)) {
    return dx >= 0 ? "east" : "west";
  }

  return dy >= 0 ? "south" : "north";
}

function buildOptionLabel(direction: string) {
  const directionTitle = direction[0]?.toUpperCase() + direction.slice(1);
  return `Probe ${directionTitle}`;
}

function canTargetCell(map: PreparedMap, x: number, y: number) {
  const key = toCellKey(x, y);

  return (
    map.walkableCells.has(key) &&
    !map.propCells.has(key) &&
    !map.characterCells.has(key) &&
    !map.playerCells.has(key)
  );
}

async function discoverMoveOptions(
  engine: RoguelikeEngineModule,
  sessionId: string,
  map: PreparedMap,
  playerId: string,
) {
  const player = map.playerFootprint;

  if (!player) {
    return [] as MoveOption[];
  }

  const rankedCells: Array<{ x: number; y: number; distance: number; direction: string }> = [];

  for (let y = 0; y < map.height; y += 1) {
    for (let x = 0; x < map.width; x += 1) {
      if (!canTargetCell(map, x, y)) {
        continue;
      }

      const dx = x - player.x;
      const dy = y - player.y;
      const distance = Math.abs(dx) + Math.abs(dy);

      if (distance === 0 || distance > 7) {
        continue;
      }

      rankedCells.push({
        x,
        y,
        distance,
        direction: classifyDirection(dx, dy),
      });
    }
  }

  rankedCells.sort((left, right) => left.distance - right.distance);

  const options: MoveOption[] = [];
  const usedDirections = new Set<string>();

  for (const candidate of rankedCells) {
    if (options.length >= 3) {
      break;
    }

    if (usedDirections.has(candidate.direction) && rankedCells.length > 3) {
      continue;
    }

    const command = createMovePlayerCommand(
      engine,
      map.mapId,
      playerId,
      candidate.x,
      candidate.y,
    );
    const envelope = await engine.executePreviewCommand(sessionId, command);
    const path = normalizePathFromEvents(envelope.Events);

    if (!isEnvelopeOk(envelope) || path.length === 0) {
      continue;
    }

    options.push({
      id: `${candidate.x}:${candidate.y}`,
      label: buildOptionLabel(candidate.direction),
      detail: `${path.length} step${path.length === 1 ? "" : "s"} to (${candidate.x}, ${candidate.y})`,
      x: candidate.x,
      y: candidate.y,
      direction: candidate.direction,
      path,
    });
    usedDirections.add(candidate.direction);
  }

  if (options.length >= 3) {
    return options;
  }

  for (const candidate of rankedCells) {
    if (options.length >= 3 || options.some((entry) => entry.id === `${candidate.x}:${candidate.y}`)) {
      continue;
    }

    const command = createMovePlayerCommand(
      engine,
      map.mapId,
      playerId,
      candidate.x,
      candidate.y,
    );
    const envelope = await engine.executePreviewCommand(sessionId, command);
    const path = normalizePathFromEvents(envelope.Events);

    if (!isEnvelopeOk(envelope) || path.length === 0) {
      continue;
    }

    options.push({
      id: `${candidate.x}:${candidate.y}`,
      label: buildOptionLabel(candidate.direction),
      detail: `${path.length} step${path.length === 1 ? "" : "s"} to (${candidate.x}, ${candidate.y})`,
      x: candidate.x,
      y: candidate.y,
      direction: candidate.direction,
      path,
    });
  }

  return options;
}

function buildPreviewStatus(option: MoveOption | null): StatusCard {
  if (!option) {
    return {
      title: "Preparing preview",
      detail: "The widget is asking the wasm build for a few valid futures.",
      tone: "muted",
      events: [],
    };
  }

  return {
    title: "Preview branch",
    detail:
      "This path was generated with the real preview command. Nothing in live history changed yet.",
    tone: "muted",
    events: [
      `command: move player to (${option.x}, ${option.y})`,
      `path length: ${option.path.length} step${option.path.length === 1 ? "" : "s"}`,
      "branch disposition: discard after render unless committed",
    ],
  };
}

function buildCommitStatus(envelope: EngineEnvelope): StatusCard {
  return {
    title: "Committed to history",
    detail: "The tracked command ran successfully and the engine advanced the live snapshot.",
    tone: "success",
    events:
      envelope.Events?.map((event) => event.Type || event.Description || "Event") ?? ["OK"],
  };
}

export default function GuidedCommandPreview() {
  const engineRef = useRef<RoguelikeEngineModule | null>(null);
  const sessionIdRef = useRef<string | null>(null);
  const optionRequestIdRef = useRef(0);

  const [state, setState] = useState<GameState | null>(null);
  const [options, setOptions] = useState<MoveOption[]>([]);
  const [selectedOptionId, setSelectedOptionId] = useState<string | null>(null);
  const [statusCard, setStatusCard] = useState<StatusCard>({
    title: "Booting engine",
    detail: "Loading the browser-local build and seeding the example world.",
    tone: "muted",
    events: [],
  });
  const [isBusy, setIsBusy] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;

    async function boot() {
      try {
        const engine = await loadEngineModule();
        const sessionEnvelope = await engine.createSession();
        const nextSessionId = sessionEnvelope.SessionId ?? null;

        if (disposed) {
          if (nextSessionId) {
            await engine.disposeSession(nextSessionId);
          }

          return;
        }

        engineRef.current = engine;
        sessionIdRef.current = nextSessionId;

        if (!nextSessionId || !isEnvelopeOk(sessionEnvelope)) {
          setErrorMessage(
            getEnvelopeError(sessionEnvelope, "Failed to create a browser-local session."),
          );
          return;
        }

        const stateEnvelope = await engine.getGameState(nextSessionId);

        if (!isEnvelopeOk(stateEnvelope) || !stateEnvelope.State) {
          setErrorMessage(
            getEnvelopeError(stateEnvelope, "Failed to load the browser-local game state."),
          );
          return;
        }

        startTransition(() => {
          setState(stateEnvelope.State ?? null);
          setErrorMessage(null);
          setStatusCard({
            title: "Session ready",
            detail: "Choose a command card to inspect the preview branch it produces.",
            tone: "muted",
            events: [],
          });
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
      const currentSessionId = sessionIdRef.current;

      if (engine && currentSessionId) {
        void engine.disposeSession(currentSessionId);
      }
    };
  }, []);

  const activeMap = getActiveMap(state);
  const playerId = getIdValue(state?.PlayerHUD?.characterUid);
  const preparedMap = useMemo(() => prepareMap(activeMap, playerId), [activeMap, playerId]);

  useEffect(() => {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;

    if (!engine || !currentSessionId || !preparedMap || !playerId) {
      return;
    }

    const requestId = optionRequestIdRef.current + 1;
    optionRequestIdRef.current = requestId;
    setIsBusy(true);

    void discoverMoveOptions(engine, currentSessionId, preparedMap, playerId)
      .then((nextOptions) => {
        if (optionRequestIdRef.current !== requestId) {
          return;
        }

        startTransition(() => {
          setOptions(nextOptions);
          const nextSelectedId = nextOptions[0]?.id ?? null;
          setSelectedOptionId(nextSelectedId);
          setStatusCard(buildPreviewStatus(nextOptions[0] ?? null));
        });
      })
      .catch((error) => {
        if (optionRequestIdRef.current !== requestId) {
          return;
        }

        setErrorMessage(error instanceof Error ? error.message : "Failed to prepare preview commands.");
      })
      .finally(() => {
        if (optionRequestIdRef.current === requestId) {
          setIsBusy(false);
        }
      });
  }, [preparedMap, playerId]);

  const selectedOption =
    options.find((option) => option.id === selectedOptionId) ?? options[0] ?? null;

  const previewPathKeys = useMemo(() => {
    const keys = new Set<string>();

    for (const step of selectedOption?.path ?? []) {
      keys.add(toCellKey(step.FromX, step.FromY));
      keys.add(toCellKey(step.ToX, step.ToY));
    }

    return keys;
  }, [selectedOption]);

  async function refreshStateFromEngine() {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;

    if (!engine || !currentSessionId) {
      return;
    }

    const stateEnvelope = await engine.getGameState(currentSessionId);

    if (!isEnvelopeOk(stateEnvelope) || !stateEnvelope.State) {
      throw new Error(getEnvelopeError(stateEnvelope, "Failed to refresh the example world."));
    }

    startTransition(() => {
      setState(stateEnvelope.State ?? null);
      setErrorMessage(null);
    });
  }

  async function handleCommit() {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;

    if (!engine || !currentSessionId || !preparedMap || !playerId || !selectedOption || isBusy) {
      return;
    }

    setIsBusy(true);

    try {
      const command = createMovePlayerCommand(
        engine,
        preparedMap.mapId,
        playerId,
        selectedOption.x,
        selectedOption.y,
      );
      const envelope = await engine.executeTrackedCommand(currentSessionId, command);

      if (!isEnvelopeOk(envelope)) {
        throw new Error(getEnvelopeError(envelope, "Commit failed."));
      }

      startTransition(() => {
        setStatusCard(buildCommitStatus(envelope));
      });

      await refreshStateFromEngine();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Commit failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function handleReset() {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;

    if (!engine || !currentSessionId || isBusy) {
      return;
    }

    setIsBusy(true);

    try {
      const envelope = await engine.resetSession(currentSessionId);

      if (!isEnvelopeOk(envelope)) {
        throw new Error(getEnvelopeError(envelope, "Reset failed."));
      }

      startTransition(() => {
        setStatusCard({
          title: "Reset session",
          detail: "The example returned to the original seed, ready to preview a different future.",
          tone: "muted",
          events: ["history root: restored to seed snapshot"],
        });
      });

      await refreshStateFromEngine();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Reset failed.");
    } finally {
      setIsBusy(false);
    }
  }

  function handleSelectOption(option: MoveOption) {
    setSelectedOptionId(option.id);
    setStatusCard(buildPreviewStatus(option));
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[0.9fr_1.1fr]">
      <div className="grid gap-4">
        <div className="rounded-3xl bg-base-200/70 p-4 sm:p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                Suggested Commands
              </p>
              <h4 className="m-0 mt-1 text-xl font-semibold text-base-content">
                Select a future to preview
              </h4>
            </div>
            <span className="badge badge-outline">
              {options.length > 0 ? `${options.length} valid branches` : "loading"}
            </span>
          </div>

          <div className="mt-4 grid gap-3">
            {options.length > 0 ? (
              options.map((option) => {
                const active = option.id === selectedOption?.id;

                return (
                  <button
                    key={option.id}
                    type="button"
                    onClick={() => handleSelectOption(option)}
                    onMouseEnter={() => handleSelectOption(option)}
                    onFocus={() => handleSelectOption(option)}
                    className={[
                      "rounded-2xl border p-4 text-left transition",
                      active
                        ? "border-accent bg-accent/10 shadow-sm"
                        : "border-base-300 bg-base-100 hover:border-accent/40",
                    ].join(" ")}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div className="text-base font-semibold text-base-content">
                        {option.label}
                      </div>
                      <span className="badge badge-accent badge-sm">
                        {option.path.length} step{option.path.length === 1 ? "" : "s"}
                      </span>
                    </div>
                    <p className="m-0 mt-2 text-sm leading-6 text-base-content/70">
                      {option.detail}
                    </p>
                  </button>
                );
              })
            ) : (
              <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
                {isBusy
                  ? "Finding a few clean moves to demonstrate..."
                  : "No preview branches are available in this state."}
              </div>
            )}
          </div>

          <div className="mt-4 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={handleCommit}
              disabled={!selectedOption || isBusy}
              className="btn btn-accent btn-sm"
            >
              Commit selected command
            </button>
            <button
              type="button"
              onClick={handleReset}
              disabled={isBusy}
              className="btn btn-outline btn-sm"
            >
              Reset session
            </button>
          </div>
        </div>

        <div className="rounded-3xl border border-base-300 bg-base-100 p-4">
          <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
            Engine Response
          </p>
          <h4 className="m-0 mt-2 text-lg font-semibold text-base-content">
            {statusCard.title}
          </h4>
          <p className="m-0 mt-2 text-sm leading-6 text-base-content/70">
            {statusCard.detail}
          </p>

          <div
            className={`mt-4 rounded-2xl p-4 font-mono text-xs leading-6 ${
              statusCard.tone === "success"
                ? "bg-success/10 text-base-content"
                : "bg-base-200/70 text-base-content/80"
            }`}
          >
            {errorMessage ? (
              <div className="text-error">{errorMessage}</div>
            ) : statusCard.events.length > 0 ? (
              statusCard.events.map((entry) => <div key={entry}>{entry}</div>)
            ) : (
              <div>Awaiting a selected command.</div>
            )}
          </div>
        </div>
      </div>

      <div className="rounded-3xl border border-base-300 bg-base-100 p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="m-0 text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
              Live State
            </p>
            <h4 className="m-0 mt-1 text-xl font-semibold text-base-content">
              Same command, real wasm build
            </h4>
          </div>
          <div className="flex flex-wrap gap-2 text-xs text-base-content/60">
            <span className="badge badge-ghost">neutral world</span>
            <span className="badge badge-accent badge-outline">preview path</span>
            <span className="badge badge-neutral">player</span>
          </div>
        </div>

        <div className="mt-4 overflow-x-auto rounded-[1.5rem] bg-base-200/70 p-3">
          {preparedMap ? (
            <div
              className="grid gap-1"
              style={{
                gridTemplateColumns: `repeat(${preparedMap.width}, 1.1rem)`,
                width: "max-content",
              }}
            >
              {Array.from({ length: preparedMap.height }).flatMap((_, y) =>
                Array.from({ length: preparedMap.width }).map((__, x) => {
                  const key = toCellKey(x, y);
                  const isWalkable = preparedMap.walkableCells.has(key);
                  const isProp = preparedMap.propCells.has(key);
                  const isItem = preparedMap.itemCells.has(key);
                  const isPlayer = preparedMap.playerCells.has(key);
                  const isCharacter = preparedMap.characterCells.has(key);
                  const inPreviewPath = previewPathKeys.has(key);
                  const isTarget =
                    selectedOption?.x === x && selectedOption?.y === y && inPreviewPath;

                  const className = [
                    "flex h-[1.1rem] w-[1.1rem] items-center justify-center rounded-[0.35rem] border text-[0.55rem] font-semibold",
                    !isWalkable || isProp
                      ? "border-base-300 bg-base-300 text-base-content/30"
                      : "border-base-300/70 bg-base-100 text-base-content/0",
                    inPreviewPath ? "border-accent/50 bg-accent/15" : "",
                    isTarget ? "border-accent bg-accent text-accent-content" : "",
                    isCharacter ? "bg-secondary/20 text-secondary" : "",
                    isItem ? "bg-warning/20 text-warning" : "",
                    isPlayer ? "border-neutral bg-neutral text-neutral-content" : "",
                  ]
                    .filter(Boolean)
                    .join(" ");

                  let label = "";

                  if (isPlayer) {
                    label = "P";
                  } else if (isCharacter) {
                    label = "G";
                  } else if (isItem) {
                    label = "I";
                  } else if (isTarget) {
                    label = "X";
                  }

                  return (
                    <div key={key} className={className}>
                      {label}
                    </div>
                  );
                }),
              )}
            </div>
          ) : (
            <div className="min-h-52 rounded-2xl border border-dashed border-base-300 bg-base-100" />
          )}
        </div>

        <p className="m-0 mt-4 text-sm leading-6 text-base-content/70">
          The cards on the left were generated by previewing real commands. The
          board on the right lets the reader see that the previewed path exists
          before the command is promoted into history.
        </p>
      </div>
    </div>
  );
}
