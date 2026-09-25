"use client";

import type { MarkerOptions,PopupOptions } from "maplibre-gl";
import * as MapLibreGL from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import {
createContext,
forwardRef,
useContext,
useEffect,
useEffectEvent,
useImperativeHandle,
useMemo,
useRef,
useState,
type ReactNode
} from "react";
import { createPortal } from "react-dom";

function cn(...inputs: Array<string | false | null | undefined>) {
  return inputs.filter(Boolean).join(" ");
}

const defaultStyles: Record<"dark" | "light", MapLibreGL.StyleSpecification> = {
  dark: {
    version: 8,
    sources: {
      "carto-dark": {
        type: "raster",
        tiles: [
          "https://a.basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}.png",
          "https://b.basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}.png",
          "https://c.basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}.png",
          "https://d.basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}.png",
        ],
        tileSize: 256,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
      },
    },
    layers: [
      {
        id: "carto-dark-layer",
        type: "raster",
        source: "carto-dark",
        minzoom: 0,
        maxzoom: 20,
      },
    ],
  },
  light: {
    version: 8,
    sources: {
      "osm-tiles": {
        type: "raster",
        tiles: [
          "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        ],
        tileSize: 256,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      },
    },
    layers: [
      {
        id: "osm-layer",
        type: "raster",
        source: "osm-tiles",
        minzoom: 0,
        maxzoom: 19,
      },
    ],
  },
};

type Theme = "light" | "dark";

type MapViewport = {
  center: [number, number];
  zoom: number;
  bearing: number;
  pitch: number;
};

type MapStyleOption = string | MapLibreGL.StyleSpecification;
type MapRef = MapLibreGL.Map;

type MapContextValue = {
  map: MapLibreGL.Map | null;
  isLoaded: boolean;
};

const MapContext = createContext<MapContextValue | null>(null);

function getDocumentTheme(): Theme | null {
  if (typeof document === "undefined") return null;
  if (document.documentElement.classList.contains("dark")) return "dark";
  if (document.documentElement.classList.contains("light")) return "light";
  return null;
}

function getSystemTheme(): Theme {
  if (typeof window === "undefined") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function useResolvedTheme(themeProp?: Theme): Theme {
  const [theme, setTheme] = useState<Theme>(() => themeProp ?? getDocumentTheme() ?? getSystemTheme());

  useEffect(() => {
    if (themeProp) return;
    const updateTheme = () => {
      const docTheme = getDocumentTheme();
      setTheme(docTheme ?? getSystemTheme());
    };
    const observer = new MutationObserver(updateTheme);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQuery.addEventListener("change", updateTheme);
    return () => {
      observer.disconnect();
      mediaQuery.removeEventListener("change", updateTheme);
    };
  }, [themeProp]);

  return themeProp ?? theme;
}

function useMap() {
  const context = useContext(MapContext);
  if (!context) throw new Error("useMap must be used within a Map component");
  return context;
}

type MapProps = {
  children?: ReactNode;
  className?: string;
  theme?: Theme;
  styles?: { light?: MapStyleOption; dark?: MapStyleOption };
  viewport?: Partial<MapViewport>;
  onViewportChange?: (viewport: MapViewport) => void;
  loading?: boolean;
} & Omit<MapLibreGL.MapOptions, "container" | "style">;

function DefaultLoader() {
  return (
    <div className="absolute inset-0 z-10 flex items-center justify-center bg-[#0F1B1A]/80 backdrop-blur-xs">
      <div className="flex gap-1.5">
        <span className="size-2 animate-pulse rounded-full bg-[#E07A3E]" />
        <span className="size-2 animate-pulse rounded-full bg-[#E07A3E] [animation-delay:150ms]" />
        <span className="size-2 animate-pulse rounded-full bg-[#E07A3E] [animation-delay:300ms]" />
      </div>
    </div>
  );
}

function getViewport(map: MapLibreGL.Map): MapViewport {
  const center = map.getCenter();
  return {
    center: [center.lng, center.lat],
    zoom: map.getZoom(),
    bearing: map.getBearing(),
    pitch: map.getPitch(),
  };
}

const Map = forwardRef<MapRef, MapProps>(function Map(
  { children, className, theme: themeProp, styles, viewport, onViewportChange, loading = false, ...props },
  ref,
) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [mapInstance, setMapInstance] = useState<MapLibreGL.Map | null>(null);
  const [isLoaded, setIsLoaded] = useState(false);
  const [isStyleLoaded, setIsStyleLoaded] = useState(false);
  const internalUpdateRef = useRef(false);
  const resolvedTheme = useResolvedTheme(themeProp);
  const currentStyleRef = useRef<MapStyleOption | null>(null);
  const notifyViewportChange = useEffectEvent((nextViewport: MapViewport) => {
    onViewportChange?.(nextViewport);
  });

  const mapStyles = useMemo(
    () => ({ dark: styles?.dark ?? defaultStyles.dark, light: styles?.light ?? defaultStyles.light }),
    [styles],
  );
  const initialMapConfigRef = useRef({ mapStyles, props, resolvedTheme, viewport });

  useImperativeHandle(ref, () => mapInstance as MapLibreGL.Map, [mapInstance]);

  useEffect(() => {
    if (!containerRef.current) return;
    const initialConfig = initialMapConfigRef.current;
    const initialStyle = initialConfig.resolvedTheme === "dark" ? initialConfig.mapStyles.dark : initialConfig.mapStyles.light;
    currentStyleRef.current = initialStyle;

    const map = new MapLibreGL.Map({
      container: containerRef.current,
      style: initialStyle,
      renderWorldCopies: false,
      attributionControl: { compact: true },
      ...initialConfig.props,
      ...initialConfig.viewport,
    });

    const loadHandler = () => {
      setIsLoaded(true);
      setIsStyleLoaded(true);
      map.resize();
    };

    const styleDataHandler = () => {
      setIsStyleLoaded(true);
    };

    const moveHandler = () => {
      if (!internalUpdateRef.current) notifyViewportChange(getViewport(map));
    };

    map.on("load", loadHandler);
    map.on("styledata", styleDataHandler);
    map.on("move", moveHandler);
    setMapInstance(map);

    // ResizeObserver to ensure tiles render when container size is computed
    const resizeObserver = new ResizeObserver(() => {
      map.resize();
    });
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }

    return () => {
      resizeObserver.disconnect();
      map.off("load", loadHandler);
      map.off("styledata", styleDataHandler);
      map.off("move", moveHandler);
      map.remove();
      setMapInstance(null);
      setIsLoaded(false);
      setIsStyleLoaded(false);
    };
  }, []);

  // Update style only when theme or style explicitly changes AFTER mount
  useEffect(() => {
    if (!mapInstance) return;
    const nextStyle = resolvedTheme === "dark" ? mapStyles.dark : mapStyles.light;
    if (currentStyleRef.current !== nextStyle) {
      currentStyleRef.current = nextStyle;
      setIsStyleLoaded(false);
      mapInstance.setStyle(nextStyle);
    }
  }, [resolvedTheme, mapStyles, mapInstance]);

  useEffect(() => {
    if (!mapInstance || !viewport) return;
    internalUpdateRef.current = true;
    mapInstance.jumpTo(viewport);
    requestAnimationFrame(() => {
      internalUpdateRef.current = false;
    });
  }, [mapInstance, viewport]);

  const contextValue = useMemo(() => ({ map: mapInstance, isLoaded: isLoaded && isStyleLoaded }), [mapInstance, isLoaded, isStyleLoaded]);

  return (
    <MapContext.Provider value={contextValue}>
      <div ref={containerRef} className={cn("relative h-full w-full", className)}>
        {(!isLoaded || loading) && <DefaultLoader />}
        {mapInstance && children}
      </div>
    </MapContext.Provider>
  );
});

type MarkerContextValue = {
  marker: MapLibreGL.Marker;
  map: MapLibreGL.Map | null;
};

const MarkerContext = createContext<MarkerContextValue | null>(null);

function useMarkerContext() {
  const context = useContext(MarkerContext);
  if (!context) throw new Error("Marker components must be used within MapMarker");
  return context;
}

type MapMarkerProps = {
  longitude: number;
  latitude: number;
  children: ReactNode;
  onClick?: (event: MouseEvent) => void;
  onMouseEnter?: (event: MouseEvent) => void;
  onMouseLeave?: (event: MouseEvent) => void;
  onDragStart?: (lngLat: { lng: number; lat: number }) => void;
  onDrag?: (lngLat: { lng: number; lat: number }) => void;
  onDragEnd?: (lngLat: { lng: number; lat: number }) => void;
} & Omit<MarkerOptions, "element">;

function MapMarker({ longitude, latitude, children, onClick, onMouseEnter, onMouseLeave, onDragStart, onDrag, onDragEnd, draggable = false, ...markerOptions }: MapMarkerProps) {
  const { map } = useMap();
  const [marker] = useState(() => new MapLibreGL.Marker({ ...markerOptions, element: document.createElement("div"), draggable }).setLngLat([longitude, latitude]));
  const handleClick = useEffectEvent((event: MouseEvent) => onClick?.(event));
  const handleMouseEnter = useEffectEvent((event: MouseEvent) => onMouseEnter?.(event));
  const handleMouseLeave = useEffectEvent((event: MouseEvent) => onMouseLeave?.(event));
  const handleDragStart = useEffectEvent(() => {
    const lngLat = marker.getLngLat();
    onDragStart?.({ lng: lngLat.lng, lat: lngLat.lat });
  });
  const handleDrag = useEffectEvent(() => {
    const lngLat = marker.getLngLat();
    onDrag?.({ lng: lngLat.lng, lat: lngLat.lat });
  });
  const handleDragEnd = useEffectEvent(() => {
    const lngLat = marker.getLngLat();
    onDragEnd?.({ lng: lngLat.lng, lat: lngLat.lat });
  });

  useEffect(() => {
    if (!map) return;
    const element = marker.getElement();
    element.addEventListener("click", handleClick);
    element.addEventListener("mouseenter", handleMouseEnter);
    element.addEventListener("mouseleave", handleMouseLeave);
    marker.on("dragstart", handleDragStart);
    marker.on("drag", handleDrag);
    marker.on("dragend", handleDragEnd);
    marker.addTo(map);
    return () => {
      element.removeEventListener("click", handleClick);
      element.removeEventListener("mouseenter", handleMouseEnter);
      element.removeEventListener("mouseleave", handleMouseLeave);
      marker.off("dragstart", handleDragStart);
      marker.off("drag", handleDrag);
      marker.off("dragend", handleDragEnd);
      marker.remove();
    };
  }, [map, marker]);

  useEffect(() => {
    if (marker.getLngLat().lng !== longitude || marker.getLngLat().lat !== latitude) marker.setLngLat([longitude, latitude]);
    if (marker.isDraggable() !== draggable) marker.setDraggable(draggable);
  }, [draggable, latitude, longitude, marker]);

  return <MarkerContext.Provider value={{ marker, map }}>{children}</MarkerContext.Provider>;
}

type MarkerContentProps = {
  children?: ReactNode;
  className?: string;
};

function MarkerContent({ children, className }: MarkerContentProps) {
  const { marker } = useMarkerContext();
  return createPortal(
    <div className={cn("relative cursor-pointer", className)}>{children || <DefaultMarkerIcon />}</div>,
    marker.getElement(),
  );
}

function DefaultMarkerIcon() {
  return <div className="relative h-4 w-4 rounded-full border-2 border-white bg-blue-500 shadow-lg" />;
}

type MarkerTooltipProps = {
  children: ReactNode;
  className?: string;
} & Omit<PopupOptions, "className" | "closeButton" | "closeOnClick">;

function MarkerTooltip({ children, className, ...popupOptions }: MarkerTooltipProps) {
  const { marker, map } = useMarkerContext();
  const container = useMemo(() => document.createElement("div"), []);
  const [tooltip] = useState(() =>
    new MapLibreGL.Popup({ offset: 16, ...popupOptions, closeOnClick: true, closeButton: false }).setMaxWidth("none"),
  );

  useEffect(() => {
    if (!map) return;
    tooltip.setDOMContent(container);
    const handleMouseEnter = () => tooltip.setLngLat(marker.getLngLat()).addTo(map);
    const handleMouseLeave = () => tooltip.remove();
    marker.getElement()?.addEventListener("mouseenter", handleMouseEnter);
    marker.getElement()?.addEventListener("mouseleave", handleMouseLeave);
    return () => {
      marker.getElement()?.removeEventListener("mouseenter", handleMouseEnter);
      marker.getElement()?.removeEventListener("mouseleave", handleMouseLeave);
      tooltip.remove();
    };
  }, [container, map, marker, tooltip]);

  useEffect(() => {
    tooltip.setOffset(popupOptions.offset ?? 16);
    if (popupOptions.maxWidth) tooltip.setMaxWidth(popupOptions.maxWidth);
  }, [popupOptions.maxWidth, popupOptions.offset, tooltip]);

  return createPortal(
    <div className={cn("pointer-events-none rounded-md bg-foreground px-2 py-1 text-xs text-balance text-background shadow-md animate-in fade-in-0 zoom-in-95 duration-200 ease-out", className)}>
      {children}
    </div>,
    container,
  );
}

type MarkerLabelProps = {
  children: ReactNode;
  className?: string;
  position?: "top" | "bottom";
};

function MarkerLabel({ children, className, position = "top" }: MarkerLabelProps) {
  const positionClasses = { top: "bottom-full mb-1", bottom: "top-full mt-1" };
  return (
    <div className={cn("absolute left-1/2 -translate-x-1/2 whitespace-nowrap text-[10px] font-medium text-foreground", positionClasses[position], className)}>
      {children}
    </div>
  );
}

export { Map,MapMarker,MarkerContent,MarkerLabel,MarkerTooltip,useMap };
