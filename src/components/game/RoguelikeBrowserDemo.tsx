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

const TILE_SIZE = 12;
const PREVIEW_DEBOUNCE_MS = 40;
const STEP_TWEEN_MS = 85;
const MOVE_COMMAND_TYPE = "MapsAPI.Commands.MoveCharacterAlongPathToCell";

function resolveThemeColor(
  styles: CSSStyleDeclaration,
  variableName: string,
  fallback: string,
) {
  const value = styles.getPropertyValue(variableName).trim();
  return value ? `oklch(${value})` : fallback;
}

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

function getPathTarget(path: Array<Required<EnginePathStep>>) {
  const lastStep = path[path.length - 1];

  if (!lastStep) {
    return null;
  }

  return { x: lastStep.ToX, y: lastStep.ToY };
}

function isEnvelopeOk(envelope: EngineEnvelope) {
  return envelope.Result?.Ok === true;
}

function getEnvelopeError(envelope: EngineEnvelope, fallback: string) {
  return envelope.Result?.ErrorMessage || fallback;
}

function buildLogEntry(command: string, envelope: EngineEnvelope): LogEntry {
  const ok = isEnvelopeOk(envelope);

  if (!ok) {
    return {
      command,
      ok: false,
      details: [getEnvelopeError(envelope, "Command failed.")],
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

function createTypedId(typeName: string, value: string | null) {
  return value ? `${typeName}:${value}` : null;
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

export default function RoguelikeBrowserDemo() {
  const engineRef = useRef<RoguelikeEngineModule | null>(null);
  const sessionIdRef = useRef<string | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const previewTimerRef = useRef<number | null>(null);
  const previewRequestIdRef = useRef(0);
  const hoveredCellKeyRef = useRef<string | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  const [sessionId, setSessionId] = useState<string | null>(null);
  const [state, setState] = useState<GameState | null>(null);
  const [logEntries, setLogEntries] = useState<LogEntry[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [previewPath, setPreviewPath] = useState<EnginePathStep[]>([]);
  const [animatedPlayerPosition, setAnimatedPlayerPosition] = useState<{
    x: number;
    y: number;
    width: number;
    height: number;
  } | null>(null);

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
        setSessionId(nextSessionId);

        if (!nextSessionId || !isEnvelopeOk(sessionEnvelope)) {
          setErrorMessage(getEnvelopeError(sessionEnvelope, "Failed to create a browser-local session."));
          return;
        }

        const stateEnvelope = await engine.getGameState(nextSessionId);

        if (!isEnvelopeOk(stateEnvelope) || !stateEnvelope.State) {
          setErrorMessage(getEnvelopeError(stateEnvelope, "Failed to load the browser-local game state."));
          return;
        }

        startTransition(() => {
          setState(stateEnvelope.State ?? null);
          setErrorMessage(null);
          setLogEntries([
            {
              command: "create session",
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

      if (previewTimerRef.current !== null) {
        window.clearTimeout(previewTimerRef.current);
      }

      if (animationFrameRef.current !== null) {
        cancelAnimationFrame(animationFrameRef.current);
      }

      const engine = engineRef.current;
      const currentSessionId = sessionIdRef.current;

      if (engine && currentSessionId) {
        void engine.disposeSession(currentSessionId);
      }
    };
  }, []);

  const activeMap = getActiveMap(state);
  const playerId = getIdValue(state?.PlayerHUD?.characterUid);
  const preparedMap = useMemo(
    () => prepareMap(activeMap, playerId),
    [activeMap, playerId],
  );

  useEffect(() => {
    const canvas = canvasRef.current;
    const map = preparedMap;

    if (!canvas || !map) {
      return;
    }

    const dpr = window.devicePixelRatio || 1;
    const cssWidth = map.width * TILE_SIZE;
    const cssHeight = map.height * TILE_SIZE;

    canvas.width = Math.max(1, Math.floor(cssWidth * dpr));
    canvas.height = Math.max(1, Math.floor(cssHeight * dpr));
    canvas.style.width = `${cssWidth}px`;
    canvas.style.height = `${cssHeight}px`;

    const context = canvas.getContext("2d");
    if (!context) {
      return;
    }

    const styles = getComputedStyle(canvas);
    const colorBase100 = resolveThemeColor(styles, "--b1", "#f8f1e4");
    const colorBase200 = resolveThemeColor(styles, "--b2", "#efe4d0");
    const colorBase300 = resolveThemeColor(styles, "--b3", "#dbc9aa");
    const colorNeutral = resolveThemeColor(styles, "--n", "#2f241f");
    const colorAccent = resolveThemeColor(styles, "--a", "#c9824b");
    const colorSecondary = resolveThemeColor(styles, "--s", "#7b9a79");
    const colorGrid = "rgba(90, 72, 54, 0.18)";

    context.setTransform(1, 0, 0, 1, 0, 0);
    context.scale(dpr, dpr);
    context.clearRect(0, 0, cssWidth, cssHeight);

    context.fillStyle = colorBase200;
    context.fillRect(0, 0, cssWidth, cssHeight);

    for (let y = 0; y < map.height; y += 1) {
      for (let x = 0; x < map.width; x += 1) {
        const key = toCellKey(x, y);
        const px = x * TILE_SIZE;
        const py = y * TILE_SIZE;

        const isWalkable = map.walkableCells.has(key);
        const playerAtCell = map.playerCells.has(key) && !animatedPlayerPosition;
        const characterAtCell = map.characterCells.has(key);
        const itemAtCell = map.itemCells.has(key);
        const propAtCell = map.propCells.has(key);

        context.fillStyle = isWalkable ? colorBase100 : colorBase300;
        context.fillRect(px, py, TILE_SIZE, TILE_SIZE);

        if (propAtCell) {
          context.fillStyle = colorBase300;
          context.fillRect(px, py, TILE_SIZE, TILE_SIZE);
          continue;
        }

        if (itemAtCell) {
          context.fillStyle = colorAccent;
          context.fillRect(px + 4, py + 4, TILE_SIZE - 8, TILE_SIZE - 8);
        }

        if (characterAtCell) {
          context.fillStyle = colorSecondary;
          context.beginPath();
          context.arc(px + TILE_SIZE / 2, py + TILE_SIZE / 2, 2.5, 0, Math.PI * 2);
          context.fill();
        }

        if (playerAtCell) {
          continue;
        }
      }
    }

    context.strokeStyle = colorGrid;
    context.lineWidth = 1;

    for (let x = 0; x <= map.width; x += 1) {
      const px = x * TILE_SIZE + 0.5;
      context.beginPath();
      context.moveTo(px, 0);
      context.lineTo(px, cssHeight);
      context.stroke();
    }

    for (let y = 0; y <= map.height; y += 1) {
      const py = y * TILE_SIZE + 0.5;
      context.beginPath();
      context.moveTo(0, py);
      context.lineTo(cssWidth, py);
      context.stroke();
    }

    const previewSteps = normalizePath(previewPath);

    if (previewSteps.length > 0) {
      context.strokeStyle = colorAccent;
      context.lineWidth = 2;
      context.beginPath();

      const firstStep = previewSteps[0]!;
      context.moveTo(
        firstStep.FromX * TILE_SIZE + TILE_SIZE / 2,
        firstStep.FromY * TILE_SIZE + TILE_SIZE / 2,
      );

      for (const step of previewSteps) {
        context.lineTo(
          step.ToX * TILE_SIZE + TILE_SIZE / 2,
          step.ToY * TILE_SIZE + TILE_SIZE / 2,
        );
      }

      context.stroke();

      const target = getPathTarget(previewSteps);
      if (target) {
        context.fillStyle = "rgba(201, 130, 75, 0.22)";
        context.beginPath();
        context.arc(
          target.x * TILE_SIZE + TILE_SIZE / 2,
          target.y * TILE_SIZE + TILE_SIZE / 2,
          TILE_SIZE * 0.34,
          0,
          Math.PI * 2,
        );
        context.fill();
      }
    }

    const renderedPlayerFootprint = animatedPlayerPosition ?? map.playerFootprint;
    if (renderedPlayerFootprint) {
      context.fillStyle = colorNeutral;
      context.fillRect(
        renderedPlayerFootprint.x * TILE_SIZE + 1,
        renderedPlayerFootprint.y * TILE_SIZE + 1,
        renderedPlayerFootprint.width * TILE_SIZE - 2,
        renderedPlayerFootprint.height * TILE_SIZE - 2,
      );
    }
  }, [animatedPlayerPosition, preparedMap, previewPath]);

  function clearPreviewPath() {
    hoveredCellKeyRef.current = null;
    previewRequestIdRef.current += 1;

    if (previewTimerRef.current !== null) {
      window.clearTimeout(previewTimerRef.current);
      previewTimerRef.current = null;
    }

    setPreviewPath([]);
  }

  async function animatePath(path: EnginePathStep[]) {
    const steps = normalizePath(path);

    if (steps.length === 0) {
      setAnimatedPlayerPosition(null);
      return;
    }

    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }

    await new Promise<void>((resolve) => {
      let stepIndex = 0;
      let stepStartTime: number | null = null;

      const tick = (timestamp: number) => {
        const currentStep = steps[stepIndex];

        if (!currentStep) {
          setAnimatedPlayerPosition(null);
          animationFrameRef.current = null;
          resolve();
          return;
        }

        if (stepStartTime === null) {
          stepStartTime = timestamp;
        }

        const rawProgress = (timestamp - stepStartTime) / STEP_TWEEN_MS;
        const progress = Math.min(1, rawProgress);
        const eased = 1 - (1 - progress) * (1 - progress);

        setAnimatedPlayerPosition({
          x: currentStep.FromX + (currentStep.ToX - currentStep.FromX) * eased,
          y: currentStep.FromY + (currentStep.ToY - currentStep.FromY) * eased,
          width: currentStep.ToWidth,
          height: currentStep.ToHeight,
        });

        if (progress >= 1) {
          stepIndex += 1;
          stepStartTime = timestamp;
        }

        animationFrameRef.current = requestAnimationFrame(tick);
      };

      animationFrameRef.current = requestAnimationFrame(tick);
    });
  }

  async function handleReset() {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;

    if (!engine || !currentSessionId || isBusy) {
      return;
    }

    setIsBusy(true);
    clearPreviewPath();
    setAnimatedPlayerPosition(null);

    try {
      const resetEnvelope = await engine.resetSession(currentSessionId);
      let stateEnvelope: EngineEnvelope | null = null;

      if (isEnvelopeOk(resetEnvelope)) {
        stateEnvelope = await engine.getGameState(currentSessionId);
      }

      startTransition(() => {
        const ok = isEnvelopeOk(resetEnvelope) && (!stateEnvelope || isEnvelopeOk(stateEnvelope));
        setErrorMessage(
          ok
            ? null
            : getEnvelopeError(
                stateEnvelope && !isEnvelopeOk(stateEnvelope) ? stateEnvelope : resetEnvelope,
                "Reset failed.",
              ),
        );

        if (stateEnvelope?.State) {
          setState(stateEnvelope.State);
        }

        setLogEntries((current) =>
          prependLogEntry(current, buildLogEntry("reset", resetEnvelope)),
        );
      });
    } finally {
      setIsBusy(false);
    }
  }

  async function handleMove(mapId: string, x: number, y: number) {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;
    const currentPlayerId = getIdValue(state?.PlayerHUD?.characterUid);

    if (!engine || !currentSessionId || !currentPlayerId || isBusy) {
      return;
    }

    setIsBusy(true);
    clearPreviewPath();

    try {
      const command = createMovePlayerCommand(engine, mapId, currentPlayerId, x, y);
      const response = await engine.executeTrackedCommand(currentSessionId, command);
      const movePath = normalizePathFromEvents(response.Events);

      if (isEnvelopeOk(response) && movePath.length > 0) {
        await animatePath(movePath);
      } else {
        setAnimatedPlayerPosition(null);
      }

      const stateEnvelope = isEnvelopeOk(response)
        ? await engine.getGameState(currentSessionId)
        : null;

      startTransition(() => {
        const ok = isEnvelopeOk(response) && (!stateEnvelope || isEnvelopeOk(stateEnvelope));
        setErrorMessage(
          ok
            ? null
            : getEnvelopeError(
                stateEnvelope && !isEnvelopeOk(stateEnvelope) ? stateEnvelope : response,
                "Move failed.",
              ),
        );

        if (stateEnvelope?.State) {
          setState(stateEnvelope.State);
        }

        setLogEntries((current) =>
          prependLogEntry(current, buildLogEntry(`move (${x}, ${y})`, response)),
        );
      });
    } finally {
      setIsBusy(false);
    }
  }

  function getCellFromPointer(
    clientX: number,
    clientY: number,
    map: PreparedMap | null,
  ) {
    const canvas = canvasRef.current;

    if (!canvas || !map) {
      return null;
    }

    const rect = canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
      return null;
    }

    const relativeX = (clientX - rect.left) / rect.width;
    const relativeY = (clientY - rect.top) / rect.height;

    if (relativeX < 0 || relativeX > 1 || relativeY < 0 || relativeY > 1) {
      return null;
    }

    const x = Math.min(map.width - 1, Math.max(0, Math.floor(relativeX * map.width)));
    const y = Math.min(map.height - 1, Math.max(0, Math.floor(relativeY * map.height)));

    return { x, y };
  }

  function canPreviewMove(map: PreparedMap, x: number, y: number) {
    const key = toCellKey(x, y);

    return (
      map.walkableCells.has(key) &&
      !map.propCells.has(key) &&
      !map.characterCells.has(key) &&
      !map.playerCells.has(key)
    );
  }

  function queuePreviewForCell(x: number, y: number) {
    const engine = engineRef.current;
    const currentSessionId = sessionIdRef.current;
    const currentPlayerId = getIdValue(state?.PlayerHUD?.characterUid);

    if (!preparedMap || !engine || !currentSessionId || !currentPlayerId || isBusy) {
      return;
    }

    const targetKey = toCellKey(x, y);
    if (hoveredCellKeyRef.current === targetKey) {
      return;
    }

    hoveredCellKeyRef.current = targetKey;

    if (previewTimerRef.current !== null) {
      window.clearTimeout(previewTimerRef.current);
    }

    const requestId = previewRequestIdRef.current + 1;
    previewRequestIdRef.current = requestId;

    previewTimerRef.current = window.setTimeout(() => {
      const command = createMovePlayerCommand(engine, preparedMap.mapId, currentPlayerId, x, y);

      void engine
        .executePreviewCommand(currentSessionId, command)
        .then((envelope) => {
          if (previewRequestIdRef.current !== requestId) {
            return;
          }

          setPreviewPath(isEnvelopeOk(envelope) ? normalizePathFromEvents(envelope.Events) : []);
        })
        .catch(() => {
          if (previewRequestIdRef.current === requestId) {
            setPreviewPath([]);
          }
        });
    }, PREVIEW_DEBOUNCE_MS);
  }

  function handleCanvasPointerMove(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!preparedMap || isBusy) {
      return;
    }

    const cell = getCellFromPointer(event.clientX, event.clientY, preparedMap);
    if (!cell || !canPreviewMove(preparedMap, cell.x, cell.y)) {
      clearPreviewPath();
      return;
    }

    queuePreviewForCell(cell.x, cell.y);
  }

  function handleCanvasPointerLeave() {
    clearPreviewPath();
  }

  function handleCanvasClick(event: React.MouseEvent<HTMLCanvasElement>) {
    if (!preparedMap || isBusy) {
      return;
    }

    const cell = getCellFromPointer(event.clientX, event.clientY, preparedMap);
    if (!cell) {
      return;
    }

    const key = toCellKey(cell.x, cell.y);
    const isWalkable = preparedMap.walkableCells.has(key);
    const blockedByProp = preparedMap.propCells.has(key);
    const blockedByCharacter = preparedMap.characterCells.has(key);
    const blockedByPlayer = preparedMap.playerCells.has(key);

    if (!isWalkable || blockedByProp || blockedByCharacter || blockedByPlayer) {
      return;
    }

    void handleMove(preparedMap.mapId, cell.x, cell.y);
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button
          type="button"
          onClick={handleReset}
          disabled={!sessionId || isBusy}
          className="rounded-full border border-base-300 bg-base-100 px-4 py-2 text-sm font-medium text-base-content transition hover:bg-base-200 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Reset
        </button>
      </div>

      <div className="overflow-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-3 shadow-sm">
        {preparedMap ? (
          <canvas
            ref={canvasRef}
            onPointerMove={handleCanvasPointerMove}
            onPointerLeave={handleCanvasPointerLeave}
            onClick={handleCanvasClick}
            className={`block max-w-none rounded-xl bg-base-200 ${
              isBusy ? "cursor-wait" : "cursor-crosshair"
            }`}
            aria-label="Roguelike map"
          />
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
