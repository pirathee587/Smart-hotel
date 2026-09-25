"use client";

import { CheckCircle2,MessageSquarePlus,Star,X } from "lucide-react";
import Image from "next/image";
import React,{ useState } from "react";

export interface ReviewItem {
  id: string;
  name: string;
  role: string;
  avatar: string;
  rating: number;
  category: "all" | "residence" | "wellness" | "critic";
  date: string;
  suite?: string;
  shortText: string;
  fullText: string;
}

const INITIAL_REVIEWS: ReviewItem[] = [
  {
    id: "1",
    name: "Donald Jackman",
    role: "Content Creator & Travel Writer",
    avatar: "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "residence",
    date: "August 2026",
    suite: "Master Slowhouse",
    shortText:
      "I've been visiting luxury sanctuaries across South Asia for years, and SmartHotel Maskeliya is simply incomparable. The automated hearth was burning upon arrival, and the view of Adam's Peak is breathtaking.",
    fullText:
      "I've been visiting luxury sanctuaries across South Asia for years, and SmartHotel Maskeliya is simply incomparable. The automated hearth was burning upon arrival, and the view of Adam's Peak is breathtaking. The keyless digital entry worked seamlessly from my phone, and having private Ceylon tea served in the heated nook was unforgettable.",
  },
  {
    id: "2",
    name: "Richard Nelson",
    role: "Instagram Influencer & Architect",
    avatar: "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "critic",
    date: "July 2026",
    suite: "Deluxe Slowhouse",
    shortText:
      "An architectural masterpiece. The way the slowhouse cantilevers into the tea ridge without disturbing the mist or forest canopy is sublime. Hyper-modern ambient technology that remains completely invisible.",
    fullText:
      "An architectural masterpiece. The way the slowhouse cantilevers into the tea ridge without disturbing the mist or forest canopy is sublime. Hyper-modern ambient technology that remains completely invisible. From underfloor geothermal heating to silent circadian lighting, every detail is considered.",
  },
  {
    id: "3",
    name: "James Washington",
    role: "Digital Content Creator",
    avatar: "https://images.unsplash.com/photo-1580489944761-15a19d654956?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "wellness",
    date: "June 2026",
    suite: "Canopy Villa",
    shortText:
      "The spring-fed thermal pools and cedar cold plunges are magical at twilight. The AI Concierge curated a private morning ridge hike that ended at a secret tea tasting table. The most rejuvenating retreat ever.",
    fullText:
      "The spring-fed thermal pools and cedar cold plunges are magical at twilight. The AI Concierge curated a private morning ridge hike that ended at a secret tea tasting table. The most rejuvenating retreat ever. I came here burnt out and left completely centered. Will return every season.",
  },
  {
    id: "4",
    name: "Elena Rostova",
    role: "Verified Guest · Master Slowhouse",
    avatar: "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "residence",
    date: "May 2026",
    suite: "Master Slowhouse",
    shortText:
      "Walking into our slowhouse with the hearth already burning and the mist drifting past the glass was the most peaceful arrival of my life. The cedar hot tub on the terrace is pure bliss.",
    fullText:
      "Walking into our slowhouse with the hearth already burning and the mist drifting past the glass was the most peaceful arrival of my life. The cedar hot tub on the terrace is pure bliss. The silence of the Maskeliya hills combined with zero-disturbance dining made this the highlight of our year.",
  },
  {
    id: "5",
    name: "Marcus Vance",
    role: "Architectural Digest Critic",
    avatar: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "critic",
    date: "April 2026",
    suite: "Forest Villa",
    shortText:
      "SmartHotel is the rare sanctuary where hyper-modern ambient technology dissolves completely into the wild serenity of the Maskeliya hills. A triumphant new benchmark for sustainable eco-luxury.",
    fullText:
      "SmartHotel is the rare sanctuary where hyper-modern ambient technology dissolves completely into the wild serenity of the Maskeliya hills. A triumphant new benchmark for sustainable eco-luxury. The thermal hydrotherapy pools and natural timber structures harmonize perfectly with Sri Lanka's highland topography.",
  },
  {
    id: "6",
    name: "Sophia Lindqvist",
    role: "Nordic Spa & Wellness Consultant",
    avatar: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&q=80",
    rating: 5,
    category: "wellness",
    date: "March 2026",
    suite: "Deluxe Slowhouse",
    shortText:
      "The 39°C geothermal mineral pools overlooking the cloud forest rival the best Alpine spas in Switzerland. The staff anticipation is telepathic yet delightfully unobtrusive.",
    fullText:
      "The 39°C geothermal mineral pools overlooking the cloud forest rival the best Alpine spas in Switzerland. The staff anticipation is telepathic yet delightfully unobtrusive. The attention to soundscapes, fresh mountain air circulation, and restorative herbal tonics is world class.",
  },
];

export function ReviewSection() {
  const [reviews, setReviews] = useState<ReviewItem[]>(INITIAL_REVIEWS);
  const [selectedCategory, setSelectedCategory] = useState<string>("all");
  const [expandedCards, setExpandedCards] = useState<Record<string, boolean>>({});
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [submittedSuccess, setSubmittedSuccess] = useState(false);

  // New review form state
  const [formData, setFormData] = useState({
    name: "",
    role: "Verified Guest",
    rating: 5,
    suite: "Master Slowhouse",
    comment: "",
  });

  const toggleExpand = (id: string) => {
    setExpandedCards((prev) => ({
      ...prev,
      [id]: !prev[id],
    }));
  };

  const filteredReviews = reviews.filter((item) => {
    if (selectedCategory === "all") return true;
    return item.category === selectedCategory;
  });

  const handleFormSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.name || !formData.comment) return;

    const newReview: ReviewItem = {
      id: Date.now().toString(),
      name: formData.name,
      role: formData.role || "Verified Guest",
      avatar: `https://api.dicebear.com/7.x/micah/svg?seed=${encodeURIComponent(formData.name)}`,
      rating: formData.rating,
      category: "residence",
      date: "Just now",
      suite: formData.suite,
      shortText: formData.comment.slice(0, 140) + (formData.comment.length > 140 ? "..." : ""),
      fullText: formData.comment,
    };

    setReviews([newReview, ...reviews]);
    setSubmittedSuccess(true);
    setTimeout(() => {
      setSubmittedSuccess(false);
      setIsModalOpen(false);
      setFormData({
        name: "",
        role: "Verified Guest",
        rating: 5,
        suite: "Master Slowhouse",
        comment: "",
      });
    }, 1500);
  };

  return (
    <section className="py-20 md:py-28 bg-[#EEE7D6] border-t border-[#6B4A3A]/15">
      <div className="max-w-7xl mx-auto px-6 md:px-12">
        {/* Top Header Block: Score & Write Review Button */}
        <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 mb-10">
          <div className="space-y-4">
            {/* Eyebrow */}
            <div className="flex items-center gap-3">
              <span className="w-6 h-[2px] bg-[#C4622D] inline-block" />
              <span className="text-xs font-semibold tracking-wider text-[#6B4A3A] uppercase">
                Guest Stories &amp; Critical Acclaim
              </span>
            </div>

            <div className="flex flex-col sm:flex-row sm:items-baseline gap-4">
              <span className="font-serif text-5xl sm:text-6xl text-[#0F1B1A] font-medium tracking-tight">
                4.98
              </span>
              <div className="space-y-1">
                {/* 5 Coral Stars */}
                <div className="flex items-center gap-1">
                  {[...Array(5)].map((_, i) => (
                    <Star
                      key={i}
                      className="w-5 h-5 fill-[#FF4A3B] text-[#FF4A3B] drop-shadow-xs"
                    />
                  ))}
                  <span className="text-xs font-semibold text-[#0F1B1A] ml-2">5.0 Star Rating</span>
                </div>
                <p className="text-xs sm:text-sm text-[#7C9188] font-light">
                  out of 5, across 300+ verified slowhouse stays in Maskeliya
                </p>
              </div>
            </div>
          </div>

          {/* Action Row: Category Filter Tabs & Write Review Button */}
          <div className="flex flex-wrap items-center gap-3">
            {/* Filter Tabs */}
            <div className="inline-flex items-center bg-[#F6F1E6] p-1 rounded-full border border-[#6B4A3A]/20 shadow-xs">
              <button
                type="button"
                onClick={() => setSelectedCategory("all")}
                className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                  selectedCategory === "all"
                    ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-xs"
                    : "text-[#3A362E]/75 hover:text-[#0F1B1A]"
                }`}
              >
                All ({reviews.length})
              </button>
              <button
                type="button"
                onClick={() => setSelectedCategory("residence")}
                className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                  selectedCategory === "residence"
                    ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-xs"
                    : "text-[#3A362E]/75 hover:text-[#0F1B1A]"
                }`}
              >
                Slowhouses
              </button>
              <button
                type="button"
                onClick={() => setSelectedCategory("wellness")}
                className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                  selectedCategory === "wellness"
                    ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-xs"
                    : "text-[#3A362E]/75 hover:text-[#0F1B1A]"
                }`}
              >
                Wellness &amp; Springs
              </button>
              <button
                type="button"
                onClick={() => setSelectedCategory("critic")}
                className={`px-3.5 py-1.5 rounded-full text-xs font-medium transition-all duration-200 cursor-pointer ${
                  selectedCategory === "critic"
                    ? "bg-[#0F1B1A] text-[#F6F1E6] shadow-xs"
                    : "text-[#3A362E]/75 hover:text-[#0F1B1A]"
                }`}
              >
                Critics &amp; Press
              </button>
            </div>

            {/* Write a Review Button */}
            <button
              type="button"
              onClick={() => setIsModalOpen(true)}
              className="inline-flex items-center gap-2 bg-[#C4622D] hover:bg-[#E07A3E] text-white text-xs font-medium px-4 py-2.5 rounded-full transition-all duration-200 shadow-sm hover:shadow-md cursor-pointer"
            >
              <MessageSquarePlus className="w-3.5 h-3.5" />
              <span>Write a Review</span>
            </button>
          </div>
        </div>

        {/* 3-Column Reviews Grid Matching The Provided Template */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredReviews.map((review) => {
            const isExpanded = expandedCards[review.id];
            return (
              <div
                key={review.id}
                className="group bg-white rounded-2xl border border-[#E9DFD2] shadow-[0_4px_20px_rgba(0,0,0,0.04)] hover:shadow-[0_12px_32px_rgba(0,0,0,0.08)] transition-all duration-300 flex flex-col justify-between overflow-hidden"
              >
                {/* 1. Header Box with Soft Rose/Warm Blush Background */}
                <div className="bg-[#FDF0EC] px-6 py-4 flex items-center gap-3.5 border-b border-[#F7E1D8]">
                  {/* Circular Avatar */}
                  <div className="relative w-12 h-12 rounded-full overflow-hidden shrink-0 border-2 border-white shadow-xs bg-[#F2DDD4]">
                    <Image
                      src={review.avatar}
                      alt={review.name}
                      fill
                      className="object-cover object-center"
                      sizes="48px"
                      unoptimized={review.avatar.includes("dicebear")}
                    />
                  </div>

                  {/* Name and Role */}
                  <div className="min-w-0 flex-1">
                    <h4 className="font-sans font-semibold text-[15px] text-[#0F1B1A] truncate tracking-tight">
                      {review.name}
                    </h4>
                    <p className="text-xs text-[#7C9188] truncate font-normal">{review.role}</p>
                  </div>
                </div>

                {/* 2. Card Body: Rating, Quote Text, and 'Read more' link */}
                <div className="p-6 flex-1 flex flex-col justify-between space-y-4">
                  <div className="space-y-3.5">
                    {/* 5 Vibrant Coral Red Stars */}
                    <div className="flex items-center gap-1">
                      {[...Array(review.rating)].map((_, i) => (
                        <Star
                          key={i}
                          className="w-4 h-4 fill-[#FF4A3B] text-[#FF4A3B]"
                        />
                      ))}
                    </div>

                    {/* Review Body Text */}
                    <p className="text-[#3A362E] text-xs sm:text-[13px] leading-relaxed font-normal">
                      {isExpanded ? review.fullText : review.shortText}
                    </p>
                  </div>

                  {/* 'Read more' / 'Read less' Link */}
                  <div className="pt-2">
                    <button
                      type="button"
                      onClick={() => toggleExpand(review.id)}
                      className="text-[#FF4A3B] hover:text-[#D9382B] text-xs font-medium cursor-pointer underline-offset-2 hover:underline inline-flex items-center gap-1 transition-colors"
                    >
                      <span>{isExpanded ? "Show less" : "Read more"}</span>
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Highlight Score Summary Badges */}
        <div className="mt-12 pt-8 border-t border-[#6B4A3A]/15 grid grid-cols-2 sm:grid-cols-4 gap-4 text-center">
          <div className="p-4 bg-[#F6F1E6] rounded-xl border border-[#6B4A3A]/10">
            <p className="font-serif text-2xl text-[#0F1B1A] font-semibold">5.0 / 5</p>
            <p className="text-[11px] text-[#7C9188] uppercase tracking-wider mt-1">Highland Quietude</p>
          </div>
          <div className="p-4 bg-[#F6F1E6] rounded-xl border border-[#6B4A3A]/10">
            <p className="font-serif text-2xl text-[#0F1B1A] font-semibold">5.0 / 5</p>
            <p className="text-[11px] text-[#7C9188] uppercase tracking-wider mt-1">Cleanliness &amp; Comfort</p>
          </div>
          <div className="p-4 bg-[#F6F1E6] rounded-xl border border-[#6B4A3A]/10">
            <p className="font-serif text-2xl text-[#0F1B1A] font-semibold">4.9 / 5</p>
            <p className="text-[11px] text-[#7C9188] uppercase tracking-wider mt-1">Thermal Hydrotherapy</p>
          </div>
          <div className="p-4 bg-[#F6F1E6] rounded-xl border border-[#6B4A3A]/10">
            <p className="font-serif text-2xl text-[#0F1B1A] font-semibold">100%</p>
            <p className="text-[11px] text-[#7C9188] uppercase tracking-wider mt-1">Verified Guest Satisfaction</p>
          </div>
        </div>
      </div>

      {/* Interactive Modal: Submit Guest Review */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-200">
          <div className="relative w-full max-w-lg bg-[#F6F1E6] border border-[#6B4A3A]/30 rounded-2xl shadow-2xl p-6 sm:p-8 space-y-6">
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b border-[#6B4A3A]/20 pb-4">
              <div className="space-y-1">
                <h3 className="font-serif text-xl font-normal text-[#0F1B1A]">Share Your Experience</h3>
                <p className="text-xs text-[#7C9188]">Your review will appear in our guest highlights.</p>
              </div>
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                className="p-1.5 text-[#7C9188] hover:text-[#0F1B1A] rounded-full hover:bg-[#EEE7D6] transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {submittedSuccess ? (
              <div className="p-6 text-center space-y-3 bg-[#16302C] text-white rounded-xl">
                <CheckCircle2 className="w-10 h-10 text-[#E07A3E] mx-auto" />
                <h4 className="font-serif text-lg font-medium">Thank you for your review!</h4>
                <p className="text-xs text-[#EEE7D6]/80">Your review has been successfully published.</p>
              </div>
            ) : (
              <form onSubmit={handleFormSubmit} className="space-y-4">
                {/* Rating selection */}
                <div>
                  <label className="block text-xs font-semibold text-[#0F1B1A] uppercase tracking-wider mb-1.5">
                    Rating
                  </label>
                  <div className="flex items-center gap-1.5">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <button
                        key={star}
                        type="button"
                        onClick={() => setFormData({ ...formData, rating: star })}
                        className="p-1 hover:scale-110 transition-transform cursor-pointer"
                      >
                        <Star
                          className={`w-6 h-6 ${
                            star <= formData.rating
                              ? "fill-[#FF4A3B] text-[#FF4A3B]"
                              : "text-stone-300"
                          }`}
                        />
                      </button>
                    ))}
                    <span className="text-xs font-medium text-[#0F1B1A] ml-2">
                      {formData.rating} out of 5 Stars
                    </span>
                  </div>
                </div>

                {/* Name */}
                <div>
                  <label className="block text-xs font-semibold text-[#0F1B1A] uppercase tracking-wider mb-1">
                    Your Name
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. Donald Jackman"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    className="w-full bg-white border border-[#6B4A3A]/30 text-sm text-[#0F1B1A] px-3.5 py-2.5 rounded-lg focus:outline-none focus:border-[#C4622D]"
                  />
                </div>

                {/* Role / Designation */}
                <div>
                  <label className="block text-xs font-semibold text-[#0F1B1A] uppercase tracking-wider mb-1">
                    Role / Title
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. Content Creator, Architect, Verified Guest"
                    value={formData.role}
                    onChange={(e) => setFormData({ ...formData, role: e.target.value })}
                    className="w-full bg-white border border-[#6B4A3A]/30 text-sm text-[#0F1B1A] px-3.5 py-2.5 rounded-lg focus:outline-none focus:border-[#C4622D]"
                  />
                </div>

                {/* Suite stayed */}
                <div>
                  <label className="block text-xs font-semibold text-[#0F1B1A] uppercase tracking-wider mb-1">
                    Residence Stayed
                  </label>
                  <select
                    value={formData.suite}
                    onChange={(e) => setFormData({ ...formData, suite: e.target.value })}
                    className="w-full bg-white border border-[#6B4A3A]/30 text-sm text-[#0F1B1A] px-3.5 py-2.5 rounded-lg focus:outline-none focus:border-[#C4622D]"
                  >
                    <option value="Master Slowhouse">Master Slowhouse</option>
                    <option value="Deluxe Slowhouse">Deluxe Slowhouse</option>
                    <option value="Forest Villa">Forest Villa</option>
                    <option value="Standard Slowhouse">Standard Slowhouse</option>
                  </select>
                </div>

                {/* Review comment */}
                <div>
                  <label className="block text-xs font-semibold text-[#0F1B1A] uppercase tracking-wider mb-1">
                    Your Review
                  </label>
                  <textarea
                    required
                    rows={4}
                    placeholder="Share your stay experience, the quiet technology, hearth warmth, or mountain views..."
                    value={formData.comment}
                    onChange={(e) => setFormData({ ...formData, comment: e.target.value })}
                    className="w-full bg-white border border-[#6B4A3A]/30 text-sm text-[#0F1B1A] p-3.5 rounded-lg focus:outline-none focus:border-[#C4622D] resize-none"
                  />
                </div>

                {/* Submit button */}
                <div className="pt-2 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setIsModalOpen(false)}
                    className="px-4 py-2.5 text-xs font-medium text-[#3A362E] hover:text-[#0F1B1A]"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="bg-[#C4622D] hover:bg-[#E07A3E] text-white text-xs font-medium px-5 py-2.5 rounded-full shadow-md transition-all duration-200 cursor-pointer"
                  >
                    Publish Review
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
