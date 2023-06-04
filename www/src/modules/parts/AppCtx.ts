export interface AppCtx {
    isRoadnikApp: boolean;
    roomId: string | null;
    tracksDataReceived: boolean;
    pointsDataReceived: boolean;
    userColorIndex: number;
    currentLayer: string | undefined;
    lastTracksOffset: number;
}

export function CreateAppCtx(): AppCtx {
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    const roomId = urlParams.get('id');

    return {
        isRoadnikApp: navigator.userAgent.includes("RoadnikApp"),
        roomId: roomId,
        tracksDataReceived: false,
        pointsDataReceived: false,
        userColorIndex: 0,
        currentLayer: undefined,
        lastTracksOffset: 0
    };
}