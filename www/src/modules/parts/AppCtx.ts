export interface IAppCtx {
    isRoadnikApp: boolean;
    roomId: string | null;
    tracksDataReceived: boolean;
    pointsDataReceived: boolean;
    userColorIndex: number;
    currentLayer: string | undefined;
    lastTracksOffset: number;
    currentLocationMarker: L.Marker | undefined;
    currentLocationCircle: L.Circle | undefined;
}

export function CreateAppCtx(): IAppCtx {
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
        lastTracksOffset: 0,
        currentLocationMarker: undefined,
        currentLocationCircle: undefined
    };
}