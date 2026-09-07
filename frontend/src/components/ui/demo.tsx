import { Map, MapMarker, MarkerContent, MarkerTooltip } from "@/components/ui/mapcn-marker-content";

const locations = [
  { id: 1, name: "SmartHotel Maskeliya Lodge", lng: 80.5750, lat: 6.8336 },
  { id: 2, name: "Highland Spring Pools", lng: 80.5780, lat: 6.8345 },
  { id: 3, name: "Adam's Peak Viewpoint", lng: 80.5840, lat: 6.8380 },
];

function MarkerContentDemo() {
  return (
    <div className="flex min-h-screen w-full items-center justify-center overflow-hidden bg-background p-8 text-foreground">
      <div className="h-[420px] w-full max-w-4xl overflow-hidden rounded-lg border shadow-sm">
        <Map center={[80.5750, 6.8336]} zoom={13}>
          {locations.map((location) => (
            <MapMarker key={location.id} longitude={location.lng} latitude={location.lat}>
              <MarkerContent>
                <div
                  data-mapcn-marker={location.name}
                  className="size-5 rounded-full border-2 border-white bg-[#C4622D] shadow-lg transition-transform hover:scale-110"
                />
              </MarkerContent>
              <MarkerTooltip>{location.name}</MarkerTooltip>
            </MapMarker>
          ))}
        </Map>
      </div>
    </div>
  );
}

export default function MarkerContentDefaultDemo() {
  return <MarkerContentDemo />;
}

export { MarkerContentDefaultDemo };
