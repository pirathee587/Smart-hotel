/**
 * SmartHotel Mock Booking Data & Service
 * // TODO: wire to Booking & Payments service
 */

import { MealPlan,RoomListing } from "../types/bookingSelection";

export const MOCK_MEAL_PLANS: MealPlan[] = [
  {
    id: "meal-all-inclusive",
    name: "All Inclusive Basis",
    fromPrice: 430,
    tagline: "Breakfast, lunch, tea tasting & 4-course dinner with estate pairings",
  },
  {
    id: "meal-half-board",
    name: "Half Board (Breakfast & Dinner)",
    fromPrice: 350,
    tagline: "Artisanal highland breakfast & multi-course evening dinner",
  },
  {
    id: "meal-bb",
    name: "Bed & Breakfast Basis",
    fromPrice: 280,
    tagline: "Farm-to-table organic breakfast at Sanctuary Hearth",
  },
  {
    id: "meal-room-only",
    name: "Room Only",
    fromPrice: 240,
    tagline: "Pure sanctuary stay, dine freely à la carte at all venues",
  },
];

export const MOCK_ROOM_LISTINGS: RoomListing[] = [
  {
    id: "room-premium-terrace",
    name: "Premium Terrace",
    imageUrl: "/images/slowhouse-hero.jpg",
    galleryImages: [
      "/images/slowhouse-hero.jpg",
      "/images/slowhouse-interior.jpg",
      "/images/hero-view.jpg",
    ],
    galleryCount: 6,
    bedCount: 1,
    sleeps: 2,
    roomSizeSqFt: 520,
    badge: "Booked in last 4 hours",
    description:
      "Expansive private timber terrace overlooking misty Maskeliya tea slopes with cedar lounge chairs and rainfall shower.",
    fullDescription:
      "Nestled into the middle tea terraces, the Premium Terrace harmoniously combines handcrafted timber joinery with gentle microclimate cooling. Features an open-air stone balcony, walk-in rainfall shower, botanical bath amenities, and an in-suite Ceylon single-origin tea station.",
    amenities: [
      "Hair Dryer",
      "Free WiFi",
      "Air Conditioning",
      "Separate Shower",
      "Balcony Terrace",
      "Espresso Machine",
      "Acoustic Insulation",
      "Keyless Entry",
    ],
    variants: [
      { id: "terrace-king", label: "Premium Terrace King", bedDescription: "1 King Bed (180 × 200 cm)" },
      { id: "terrace-twin", label: "Premium Terrace Twin", bedDescription: "2 Twin XL Beds (100 × 200 cm)" },
    ],
    ratePlans: [
      {
        id: "rp-terrace-member",
        name: "Member Rate — All Meals Included",
        inclusionsLabel: "All Meals Included",
        bullets: [
          "SmartHotel Member Perk: 15% savings applied automatically",
          "Chef-prepared breakfast, lunch & 3-course dinner included",
          "Complimentary afternoon Ceylon tea ceremony with pastry pairing",
          "Free cancellation up to 48 hours prior to check-in",
        ],
        originalPrice: 380,
        price: 315,
        currency: "USD",
        isMemberRate: true,
      },
      {
        id: "rp-terrace-flex",
        name: "Standard Best Flexible — Bed & Breakfast",
        inclusionsLabel: "Breakfast Included",
        bullets: [
          "Farm-to-table breakfast served overlooking tea valleys",
          "Uncapped high-speed Starlink WiFi throughout the property",
          "Early check-in & late check-out subject to availability",
        ],
        originalPrice: 330,
        price: 275,
        currency: "USD",
        isMemberRate: false,
      },
    ],
    variantRatePlans: {
      "terrace-king": [
        {
          id: "rp-terrace-king-member",
          name: "Member Rate — All Meals Included",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "SmartHotel Member Perk: 15% savings applied automatically",
            "Chef-prepared breakfast, lunch & 3-course dinner included",
            "Complimentary afternoon Ceylon tea ceremony with pastry pairing",
            "Free cancellation up to 48 hours prior to check-in",
          ],
          originalPrice: 380,
          price: 315,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-terrace-king-flex",
          name: "Standard Best Flexible — Bed & Breakfast",
          inclusionsLabel: "Breakfast Included",
          bullets: [
            "Farm-to-table breakfast served overlooking tea valleys",
            "Uncapped high-speed Starlink WiFi throughout the property",
            "Early check-in & late check-out subject to availability",
          ],
          originalPrice: 330,
          price: 275,
          currency: "USD",
          isMemberRate: false,
        },
      ],
      "terrace-twin": [
        {
          id: "rp-terrace-twin-member",
          name: "Member Rate — Twin Comfort Plan",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Two plush plush ergonomic twin beds with hypoallergenic down",
            "Full breakfast and evening dining included for 2 guests",
            "Member perk: complimentary estate mountain bike hire",
            "Flexible cancellation up to 48 hours before stay",
          ],
          originalPrice: 370,
          price: 310,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-terrace-twin-flex",
          name: "Flexible Room & Breakfast",
          inclusionsLabel: "Breakfast Included",
          bullets: [
            "Daily organic estate breakfast for both guests",
            "Dedicated work desk with high-speed fiber connection",
          ],
          originalPrice: 320,
          price: 270,
          currency: "USD",
          isMemberRate: false,
        },
      ],
    },
  },
  {
    id: "room-slowhouse-suite",
    name: "Signature Slowhouse Suite",
    imageUrl: "/images/slowhouse-interior.jpg",
    galleryImages: [
      "/images/slowhouse-interior.jpg",
      "/images/slowhouse-hero.jpg",
      "/images/keyless-entry.jpg",
    ],
    galleryCount: 8,
    bedCount: 1,
    sleeps: 3,
    roomSizeSqFt: 780,
    badge: "Only 2 suites left for these dates",
    description:
      "Architectural slowhouse featuring exposed rammed-earth walls, private geothermal cedar tub, and roaring evening fireplace.",
    fullDescription:
      "A sanctuary of acoustic tranquility and tactile comfort. The Signature Slowhouse Suite features double-height glazed openings framing Sri Pada peak, a sunken fireside lounge, and a private spring-fed cedar soaking tub fed directly by mineral aquifers.",
    amenities: [
      "Hair Dryer",
      "Free WiFi",
      "Air Conditioning",
      "Separate Shower",
      "Geothermal Soak Tub",
      "Fireplace Lounge",
      "Private Cellar Fridge",
      "Mountain View Glazing",
    ],
    variants: [
      { id: "slowhouse-king", label: "Signature Slowhouse King", bedDescription: "1 Master King Bed" },
      { id: "slowhouse-super-king", label: "Signature Slowhouse Super King", bedDescription: "1 Super King Bed + Daybed" },
    ],
    ratePlans: [
      {
        id: "rp-slowhouse-member",
        name: "Member Exclusive — All Inclusive & Geothermal Ritual",
        inclusionsLabel: "All Meals Included",
        bullets: [
          "Curated 4-course evening dining & highland wine pairing",
          "Evening cedar soak ritual prepared by our in-house bath sommelier",
          "Member exclusive: 20% discount on guided Adams Peak treks",
          "Zero cancellation fee up to 72 hours prior to arrival",
        ],
        originalPrice: 520,
        price: 430,
        currency: "USD",
        isMemberRate: true,
      },
      {
        id: "rp-slowhouse-halfboard",
        name: "Highland Retreat — Half Board Basis",
        inclusionsLabel: "Breakfast & Dinner Included",
        bullets: [
          "Daily gourmet breakfast and 3-course evening dinner",
          "Complimentary firewood replenishment for in-suite hearth",
          "Complimentary afternoon tea & fresh Ceylon scones",
        ],
        originalPrice: 470,
        price: 395,
        currency: "USD",
        isMemberRate: false,
      },
    ],
    variantRatePlans: {
      "slowhouse-king": [
        {
          id: "rp-slowhouse-king-member",
          name: "Member Exclusive — All Inclusive & Geothermal Ritual",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Curated 4-course evening dining & highland wine pairing",
            "Evening cedar soak ritual prepared by our in-house bath sommelier",
            "Member exclusive: 20% discount on guided Adams Peak treks",
            "Zero cancellation fee up to 72 hours prior to arrival",
          ],
          originalPrice: 520,
          price: 430,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-slowhouse-king-halfboard",
          name: "Highland Retreat — Half Board Basis",
          inclusionsLabel: "Breakfast & Dinner Included",
          bullets: [
            "Daily gourmet breakfast and 3-course evening dinner",
            "Complimentary firewood replenishment for in-suite hearth",
            "Complimentary afternoon tea & fresh Ceylon scones",
          ],
          originalPrice: 470,
          price: 395,
          currency: "USD",
          isMemberRate: false,
        },
      ],
      "slowhouse-super-king": [
        {
          id: "rp-slowhouse-super-member",
          name: "Member Exclusive — Grand Suite All Inclusive",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Super King bedding plus daybed for a third guest",
            "All-inclusive dining for up to 3 guests included",
            "Complimentary private herbal compress session",
            "Priority check-in & personalized welcome tea vintage",
          ],
          originalPrice: 580,
          price: 485,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-slowhouse-super-flex",
          name: "Grand Suite — Bed & Breakfast",
          inclusionsLabel: "Breakfast Included",
          bullets: [
            "Full gourmet breakfast served in-suite or at Hearth",
            "Spacious lounge seating for leisurely reading",
          ],
          originalPrice: 510,
          price: 425,
          currency: "USD",
          isMemberRate: false,
        },
      ],
    },
  },
  {
    id: "room-ridge-panorama",
    name: "Ridge Panorama Suite",
    imageUrl: "/images/ridge-thumb.jpg",
    galleryImages: [
      "/images/ridge-thumb.jpg",
      "/images/hero-view.jpg",
      "/images/slowhouse-hero.jpg",
    ],
    galleryCount: 5,
    bedCount: 2,
    sleeps: 4,
    roomSizeSqFt: 920,
    badge: "Booked in last 2 hours",
    description:
      "Elevated high atop the southern ridge line with unobstructed 270° panoramic views over the Maskeliya reservoir.",
    fullDescription:
      "Perched along the high windswept ridge, these dual-bedroom suites offer uninterrupted sunset views across Maussakelle reservoir. Featuring natural slate finishes, twin rainfall showers, and an expansive cantilevered timber observation deck.",
    amenities: [
      "Hair Dryer",
      "Free WiFi",
      "Air Conditioning",
      "Separate Shower",
      "Panoramic Ridge Deck",
      "Dual En-suite Bathrooms",
      "Stargazing Telescope",
      "Keyless Digital Access",
    ],
    variants: [
      { id: "ridge-king-double", label: "Ridge King & Double", bedDescription: "1 King Bed + 1 Queen Bed" },
      { id: "ridge-twin-queens", label: "Ridge Twin Queens", bedDescription: "2 Luxury Queen Beds" },
    ],
    ratePlans: [
      {
        id: "rp-ridge-member",
        name: "Member Rate — Ridge Sanctuary Package",
        inclusionsLabel: "All Meals Included",
        bullets: [
          "Full breakfast, ridge picnic basket lunch & 4-course dinner",
          "Private sunset cocktail service on your observation deck",
          "Member perk: late 2:00 PM check-out guaranteed",
          "Complimentary telescope guide session with local naturalist",
        ],
        originalPrice: 620,
        price: 495,
        currency: "USD",
        isMemberRate: true,
      },
      {
        id: "rp-ridge-flex",
        name: "Ridge Suite — Half Board Basis",
        inclusionsLabel: "Breakfast & Dinner Included",
        bullets: [
          "Gourmet breakfast and evening dinner for all occupants",
          "Tea valley panorama view from every bedroom window",
          "Free cancellation up to 5 days before check-in",
        ],
        originalPrice: 560,
        price: 450,
        currency: "USD",
        isMemberRate: false,
      },
    ],
    variantRatePlans: {
      "ridge-king-double": [
        {
          id: "rp-ridge-kd-member",
          name: "Member Rate — Ridge Sanctuary Package",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Full breakfast, ridge picnic basket lunch & 4-course dinner",
            "Private sunset cocktail service on your observation deck",
            "Member perk: late 2:00 PM check-out guaranteed",
            "Complimentary telescope guide session with local naturalist",
          ],
          originalPrice: 620,
          price: 495,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-ridge-kd-flex",
          name: "Ridge Suite — Half Board Basis",
          inclusionsLabel: "Breakfast & Dinner Included",
          bullets: [
            "Gourmet breakfast and evening dinner for all occupants",
            "Tea valley panorama view from every bedroom window",
            "Free cancellation up to 5 days before check-in",
          ],
          originalPrice: 560,
          price: 450,
          currency: "USD",
          isMemberRate: false,
        },
      ],
      "ridge-twin-queens": [
        {
          id: "rp-ridge-tq-member",
          name: "Member Rate — Twin Queens Experience",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Two spacious queen beds for up to 4 adults",
            "All inclusive farm meals and tea rituals included",
            "Complimentary ridge photography session",
          ],
          originalPrice: 630,
          price: 505,
          currency: "USD",
          isMemberRate: true,
        },
      ],
    },
  },
  {
    id: "room-canopy-villa",
    name: "Canopy Geothermal Villa",
    imageUrl: "/images/pool-thumb.jpg",
    galleryImages: [
      "/images/pool-thumb.jpg",
      "/images/slowhouse-interior.jpg",
      "/images/hero-view.jpg",
    ],
    galleryCount: 10,
    bedCount: 2,
    sleeps: 4,
    roomSizeSqFt: 1250,
    badge: "Top Rated Villa (4.98 ★)",
    description:
      "Freestanding two-pavilion residence enveloped in virgin cloud forest, boasting a private geothermal heated plunge pool.",
    fullDescription:
      "Our pinnacle private living experience. Designed for guests seeking ultimate seclusion and immersion in Central Highlands ecology. Features private thermal pool maintained at 38°C, dedicated estate butler, outdoor rain courtyard, and bespoke in-villa dining.",
    amenities: [
      "Hair Dryer",
      "Free WiFi",
      "Air Conditioning",
      "Separate Shower",
      "Heated Thermal Plunge Pool",
      "Private Butler Service",
      "In-Villa Chef Dining",
      "Floating Breakfast Deck",
    ],
    variants: [
      { id: "canopy-master-king", label: "Canopy Master King Pavilion", bedDescription: "2 King Bed Pavilions" },
      { id: "canopy-family-pavilion", label: "Canopy Family Pavilion", bedDescription: "1 King + 2 Twin XL Beds" },
    ],
    ratePlans: [
      {
        id: "rp-canopy-ultra",
        name: "Member Rate — Ultra All-Inclusive Villa Haven",
        inclusionsLabel: "All Meals Included",
        bullets: [
          "All meals, private in-villa chef dining & reserve tea cellar pairings",
          "Dedicated personal butler on call 24/7",
          "Complimentary roundtrip private transfer (Colombo / Kandy)",
          "Daily in-villa Ayurvedic herbal compress & massage treatments",
        ],
        originalPrice: 950,
        price: 790,
        currency: "USD",
        isMemberRate: true,
      },
      {
        id: "rp-canopy-bb",
        name: "Private Pavilion — Bed & Breakfast Retreat",
        inclusionsLabel: "Breakfast Included",
        bullets: [
          "Floating breakfast served directly in the heated geothermal pool",
          "Personal butler assistance throughout your stay",
          "Complimentary garment pressing and mountain e-bikes",
        ],
        originalPrice: 820,
        price: 680,
        currency: "USD",
        isMemberRate: false,
      },
    ],
    variantRatePlans: {
      "canopy-master-king": [
        {
          id: "rp-canopy-mk-ultra",
          name: "Member Rate — Ultra All-Inclusive Villa Haven",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "All meals, private in-villa chef dining & reserve tea cellar pairings",
            "Dedicated personal butler on call 24/7",
            "Complimentary roundtrip private transfer (Colombo / Kandy)",
            "Daily in-villa Ayurvedic herbal compress & massage treatments",
          ],
          originalPrice: 950,
          price: 790,
          currency: "USD",
          isMemberRate: true,
        },
        {
          id: "rp-canopy-mk-bb",
          name: "Private Pavilion — Bed & Breakfast Retreat",
          inclusionsLabel: "Breakfast Included",
          bullets: [
            "Floating breakfast served directly in the heated geothermal pool",
            "Personal butler assistance throughout your stay",
            "Complimentary garment pressing and mountain e-bikes",
          ],
          originalPrice: 820,
          price: 680,
          currency: "USD",
          isMemberRate: false,
        },
      ],
      "canopy-family-pavilion": [
        {
          id: "rp-canopy-family-ultra",
          name: "Member Rate — Family Estate All Inclusive",
          inclusionsLabel: "All Meals Included",
          bullets: [
            "Full villa access for 4 guests with private heated pool",
            "Family-style organic feasts prepared in-villa by estate chef",
            "Nature foraging walk and junior tea tasting experience",
          ],
          originalPrice: 990,
          price: 825,
          currency: "USD",
          isMemberRate: true,
        },
      ],
    },
  },
];

/**
 * Mock Booking Service
 * Stubbed endpoint helpers ready to swap for real API.
 */
export const mockBookingService = {
  // TODO: wire to Booking & Payments service
  async getMealPlans(): Promise<MealPlan[]> {
    return new Promise((resolve) => {
      setTimeout(() => resolve(MOCK_MEAL_PLANS), 50);
    });
  },

  // TODO: wire to Booking & Payments service
  async getRoomListings(): Promise<RoomListing[]> {
    return new Promise((resolve) => {
      setTimeout(() => resolve(MOCK_ROOM_LISTINGS), 50);
    });
  },
};
