export interface IAppCtx {
    readonly isRoadnikApp: boolean;
    readonly roomId: string | null;
    lastTracksOffset: number;
    currentLocationMarker: L.Marker | undefined;
    currentLocationCircle: L.Circle | undefined;
    userColorIndex: number;
    readonly userColors: Map<string, string>;
    selectedTrack: string | null;
    firstTracksSyncCompleted: boolean;
    maxTrackPoints: number;
}

export function CreateAppCtx(): IAppCtx {
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);

    return {
        isRoadnikApp: navigator.userAgent.includes("RoadnikApp"),
        roomId: urlParams.get('id'),
        lastTracksOffset: 0,
        currentLocationMarker: undefined,
        currentLocationCircle: undefined,
        userColorIndex: 0,
        userColors: new Map<string, string>(),
        selectedTrack: null,
        firstTracksSyncCompleted: false,
        maxTrackPoints: 1000,
    };
}