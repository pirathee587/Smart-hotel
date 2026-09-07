"use client";

import React, { useState } from "react";
import { Search, Calendar, User, MessageCircle, ChevronRight } from "lucide-react";

const blogPosts = [
  {
    id: 1,
    title: "Google inks pact for new 35-storey office in city center",
    image: "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&q=80&w=800",
    date: "15 Jan",
    category: "Corporate",
    author: "Admin",
    comments: 12,
    excerpt: "The tech giant Google has signed a new lease for a state-of-the-art office skyscraper, highlighting corporate interest in smart buildings and high-tech workspaces. Here is what it means for premium business travels...",
  },
  {
    id: 2,
    title: "Cozy winter stays and fireside experiences in luxury cabins",
    image: "https://images.unsplash.com/photo-1584132967334-10e028bd69f7?auto=format&fit=crop&q=80&w=800",
    date: "12 Jan",
    category: "Travel",
    author: "Concierge",
    comments: 8,
    excerpt: "With the seasonal chill settling in, smart resorts are adapting their guest rooms with automated thermal settings, personalized fireside amenities, and seasonal warm dining options. Discover our winter escapes...",
  },
  {
    id: 3,
    title: "10 Travel trends shaping high-end hotel resorts in 2026",
    image: "https://images.unsplash.com/photo-1571896349842-33c89424de2d?auto=format&fit=crop&q=80&w=800",
    date: "10 Jan",
    category: "Hospitality",
    author: "Staff",
    comments: 15,
    excerpt: "From staff-less registration and automated dining systems to organic oceanfront wellness setups, premium resorts are redefining hospitality in 2026. Here are the 10 trends to watch this season...",
  },
];

const categories = [
  { name: "Resort Experiences", count: 24 },
  { name: "Dining & Culinary", count: 18 },
  { name: "Spa & Wellness", count: 12 },
  { name: "Smart Technology", count: 15 },
  { name: "Corporate Travels", count: 9 },
];

const recentPosts = [
  { title: "Smart systems integration for suites", date: "Jan 18, 2026", img: "https://images.unsplash.com/photo-1520250497591-112f2f40a3f4?auto=format&fit=crop&q=80&w=150" },
  { title: "Designing luxury infinity pools", date: "Jan 14, 2026", img: "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&q=80&w=150" },
  { title: "Organic culinary menu highlights", date: "Jan 11, 2026", img: "https://images.unsplash.com/photo-1578683010236-d716f9a3f461?auto=format&fit=crop&q=80&w=150" },
];

const tags = ["Hotel", "Travel", "Luxury", "Spa", "Dining", "Suite", "Technology", "Ocean"];

const instagramFeeds = [
  "https://images.unsplash.com/photo-1506929562872-bb421503ef21?auto=format&fit=crop&q=80&w=150",
  "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=150",
  "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?auto=format&fit=crop&q=80&w=150",
  "https://images.unsplash.com/photo-1505118380757-91f5f5632de0?auto=format&fit=crop&q=80&w=150",
  "https://images.unsplash.com/photo-1519046904884-53103b34b206?auto=format&fit=crop&q=80&w=150",
  "https://images.unsplash.com/photo-1542314831-068cd1dbfeeb?auto=format&fit=crop&q=80&w=150",
];

export default function BlogPage() {
  const [search, setSearch] = useState("");

  return (
    <div className="flex flex-col w-full text-text-primary bg-bg-dark transition-colors duration-300">
      
      {/* Hero Banner */}
      <section className="relative h-[45vh] w-full flex items-center justify-center overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage:
              "url('https://images.unsplash.com/photo-1542314831-068cd1dbfeeb?auto=format&fit=crop&q=80&w=1600')",
          }}
        />
        <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-transparent to-bg-dark dark:from-black/30 dark:via-black/25 dark:to-[#070c17] transition-all duration-300" />
        <div className="relative z-10 text-center px-6">
          <span className="text-primary text-xs font-semibold tracking-[0.25em] uppercase mb-3 block">
            Resort Journal
          </span>
          <h1 className="font-serif font-bold text-4xl sm:text-5xl text-[#1B2A4A] dark:text-white tracking-wide">
            Blog
          </h1>
        </div>
      </section>

      {/* Main layout with sidebar */}
      <section className="py-20 max-w-7xl mx-auto px-6 grid gap-12 md:grid-cols-12 w-full">
        
        {/* Left Column - Blog Posts */}
        <div className="md:col-span-8 space-y-12">
          {blogPosts.map((post) => (
            <div key={post.id} className="relative group border-b border-card-border pb-12 last:border-0 last:pb-0">
              {/* Image banner */}
              <div className="relative aspect-[21/9] overflow-hidden rounded-xl border border-card-border mb-6 bg-slate-900 shadow-sm">
                <img
                  src={post.image}
                  alt={post.title}
                  className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-102"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-black/55 to-transparent" />
                
                {/* Date Badge */}
                <div className="absolute bottom-4 left-4 bg-primary text-white flex flex-col items-center justify-center px-3.5 py-1.5 rounded-lg shadow-lg font-serif font-bold leading-tight">
                  <span className="text-base">{post.date.split(" ")[0]}</span>
                  <span className="text-[10px] uppercase tracking-wider text-white/80">{post.date.split(" ")[1]}</span>
                </div>
              </div>

              {/* Text content */}
              <div className="space-y-3">
                <h2 className="font-serif font-bold text-xl sm:text-2xl text-text-primary hover:text-primary transition-colors cursor-pointer leading-tight">
                  {post.title}
                </h2>
                
                {/* Metadata */}
                <div className="flex items-center gap-4 text-xs text-text-secondary pt-1 opacity-80">
                  <span className="flex items-center gap-1"><User className="size-3.5 text-primary" /> By {post.author}</span>
                  <span className="flex items-center gap-1"><MessageCircle className="size-3.5 text-primary" /> {post.comments} Comments</span>
                  <span className="px-2 py-0.5 rounded bg-white/5 border border-card-border font-semibold text-[10px] text-primary uppercase tracking-wider">{post.category}</span>
                </div>

                <p className="text-text-secondary text-xs sm:text-sm leading-relaxed pt-2">
                  {post.excerpt}
                </p>
                
                <div className="pt-2">
                  <button className="flex items-center gap-1.5 text-xs font-semibold text-primary hover:text-text-primary transition-colors cursor-pointer uppercase tracking-wider">
                    Read More
                    <ChevronRight className="size-3" />
                  </button>
                </div>
              </div>
            </div>
          ))}

          {/* Simple Pagination */}
          <div className="flex items-center justify-center gap-2 pt-8">
            <button className="w-8 h-8 rounded border border-card-border flex items-center justify-center text-xs hover:border-primary hover:text-primary transition-colors cursor-pointer bg-card-dark text-text-primary">&lt;</button>
            <button className="w-8 h-8 rounded border border-primary bg-primary/10 flex items-center justify-center text-xs text-primary font-bold">1</button>
            <button className="w-8 h-8 rounded border border-card-border flex items-center justify-center text-xs hover:border-primary hover:text-primary transition-colors cursor-pointer bg-card-dark text-text-primary">2</button>
            <button className="w-8 h-8 rounded border border-card-border flex items-center justify-center text-xs hover:border-primary hover:text-primary transition-colors cursor-pointer bg-card-dark text-text-primary">&gt;</button>
          </div>
        </div>

        {/* Right Column - Sidebar */}
        <div className="md:col-span-4 space-y-10">
          
          {/* 1. Search */}
          <div className="p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md">
            <h3 className="font-serif font-semibold text-sm text-text-primary uppercase border-b border-card-border pb-2 mb-4 tracking-wider">
              Search Journal
            </h3>
            <div className="relative">
              <input
                type="text"
                placeholder="Search..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full bg-white dark:bg-[#0b0f1a] border border-card-border dark:border-white/10 rounded-lg pl-3 pr-10 py-2.5 text-xs text-text-primary placeholder-slate-500 focus:outline-none focus:border-primary transition-colors h-10 shadow-sm"
              />
              <button className="absolute right-3 top-3 text-text-secondary hover:text-primary cursor-pointer">
                <Search className="size-4" />
              </button>
            </div>
          </div>

          {/* 2. Categories */}
          <div className="p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md">
            <h3 className="font-serif font-semibold text-sm text-text-primary uppercase border-b border-card-border pb-2 mb-4 tracking-wider">
              Categories
            </h3>
            <ul className="space-y-3">
              {categories.map((cat, idx) => (
                <li key={idx} className="flex justify-between items-center text-xs text-text-secondary hover:text-primary transition-colors cursor-pointer">
                  <span>{cat.name}</span>
                  <span className="text-[10px] bg-white/5 dark:bg-black/10 border border-card-border px-2 py-0.5 rounded font-mono text-text-secondary opacity-75">{cat.count}</span>
                </li>
              ))}
            </ul>
          </div>

          {/* 3. Recent Posts */}
          <div className="p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md">
            <h3 className="font-serif font-semibold text-sm text-text-primary uppercase border-b border-card-border pb-2 mb-4 tracking-wider">
              Recent Posts
            </h3>
            <div className="space-y-4">
              {recentPosts.map((post, idx) => (
                <div key={idx} className="flex items-center gap-3.5 cursor-pointer group">
                  <img
                    src={post.img}
                    alt={post.title}
                    className="object-cover w-11 h-11 rounded-lg border border-card-border dark:border-white/5 shrink-0"
                  />
                  <div className="overflow-hidden">
                    <h4 className="text-xs font-semibold text-text-primary group-hover:text-primary transition-colors line-clamp-2 leading-tight">
                      {post.title}
                    </h4>
                    <span className="text-[9px] text-text-secondary flex items-center gap-1 mt-0.5"><Calendar className="size-2.5 text-primary" /> {post.date}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* 4. Tag Cloud */}
          <div className="p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md">
            <h3 className="font-serif font-semibold text-sm text-text-primary uppercase border-b border-card-border pb-2 mb-4 tracking-wider">
              Tag Cloud
            </h3>
            <div className="flex flex-wrap gap-2 pt-1">
              {tags.map((tag, idx) => (
                <span
                  key={idx}
                  className="px-2.5 py-1 text-[10px] rounded bg-white/5 dark:bg-black/10 hover:bg-primary hover:text-white transition-colors cursor-pointer border border-card-border text-text-secondary"
                >
                  {tag}
                </span>
              ))}
            </div>
          </div>

          {/* 5. Instagram Feeds */}
          <div className="p-6 bg-card-dark dark:bg-[#0a0f1d] border border-card-border dark:border-white/5 rounded-xl shadow-md">
            <h3 className="font-serif font-semibold text-sm text-text-primary uppercase border-b border-card-border pb-2 mb-4 tracking-wider">
              Instagram Feeds
            </h3>
            <div className="grid grid-cols-3 gap-2.5">
              {instagramFeeds.map((feed, idx) => (
                <div key={idx} className="relative aspect-square rounded-lg overflow-hidden border border-card-border cursor-pointer group">
                  <img
                    src={feed}
                    alt=""
                    className="object-cover w-full h-full transition-transform duration-500 group-hover:scale-105"
                  />
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
