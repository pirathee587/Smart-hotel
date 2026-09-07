"use client";

import React, { useState } from "react";
import { Mail, Phone, MapPin, Globe, Send, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/shadcn-button";

export default function ContactPage() {
  const [message, setMessage] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState("");
  const [sending, setSending] = useState(false);
  const [success, setSuccess] = useState(false);

  const handleSend = (e: React.FormEvent) => {
    e.preventDefault();
    setSending(true);
    setTimeout(() => {
      setSending(false);
      setSuccess(true);
      setMessage("");
      setName("");
      setEmail("");
      setSubject("");
      setTimeout(() => setSuccess(false), 3000);
    }, 1500);
  };

  return (
    <div className="flex flex-col w-full text-text-primary bg-bg-dark transition-colors duration-300">
      
      {/* Hero Banner */}
      <section className="relative h-[45vh] w-full flex items-center justify-center overflow-hidden">
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{
            backgroundImage:
              "url('https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&q=80&w=1600')",
          }}
        />
        <div className="absolute inset-0 bg-gradient-to-b from-black/10 via-transparent to-bg-dark dark:from-black/30 dark:via-black/25 dark:to-[#070c17] transition-all duration-300" />
        <div className="relative z-10 text-center px-6">
          <span className="text-primary text-xs font-semibold tracking-[0.25em] uppercase mb-3 block">
            Contact Concierge
          </span>
          <h1 className="font-serif font-bold text-4xl sm:text-5xl text-[#1B2A4A] dark:text-white tracking-wide">
            Get In Touch
          </h1>
        </div>
      </section>

      {/* Styled Luxury Map Placeholder */}
      <section className="max-w-7xl mx-auto px-6 w-full pt-20">
        <div className="relative w-full h-[320px] rounded-xl overflow-hidden border border-card-border bg-card-dark dark:bg-[#0a0f1d] flex flex-col items-center justify-center shadow-lg dark:shadow-2xl transition-all duration-300">
          {/* Subtle grid background effect */}
          <div className="absolute inset-0 opacity-15 bg-[radial-gradient(#6366f1_1px,transparent_1px)] [background-size:16px_16px]" />
          
          <div className="relative z-10 text-center px-6 flex flex-col items-center">
            <div className="w-12 h-12 bg-primary/10 border border-primary/20 rounded-full flex items-center justify-center text-primary mb-4 animate-bounce">
              <MapPin className="size-6" />
            </div>
            <h3 className="font-serif font-bold text-lg text-text-primary mb-2">SmartHotel GPS Location</h3>
            <p className="text-xs text-text-secondary max-w-sm mb-4">
              Latitude: 40.7128° N, Longitude: -74.0060° W <br />
              200, Green road, Mongla, New York, USA
            </p>
            <button className="px-5 py-2 border border-card-border hover:border-primary text-text-secondary hover:text-text-primary rounded text-xs font-medium transition-colors cursor-pointer bg-white/5 dark:bg-black/10">
              Open in Google Maps
            </button>
          </div>
        </div>
      </section>

      {/* Form / Details Column Section */}
      <section className="py-20 max-w-7xl mx-auto px-6 grid gap-16 md:grid-cols-12 w-full">
        {/* Contact Form Left Column */}
        <div className="md:col-span-8">
          <h2 className="font-serif font-bold text-2xl text-text-primary mb-8 flex items-center gap-2">
            <MessageSquare className="size-5 text-primary" />
            Send Us A Message
          </h2>
          
          <form onSubmit={handleSend} className="space-y-4">
            <div className="flex flex-col gap-1.5">
              <textarea
                placeholder="Enter your message..."
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                rows={6}
                className="w-full bg-card-dark dark:bg-[#0b0f1a]/80 border border-card-border dark:border-white/10 rounded-lg p-3 text-sm text-text-primary placeholder-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all resize-none shadow-sm"
                required
              />
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <input
                  type="text"
                  placeholder="Enter your name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full bg-card-dark dark:bg-[#0b0f1a]/80 border border-card-border dark:border-white/10 rounded-lg px-3 py-2.5 text-sm text-text-primary placeholder-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all h-10 shadow-sm"
                  required
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <input
                  type="email"
                  placeholder="Enter email address"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full bg-card-dark dark:bg-[#0b0f1a]/80 border border-card-border dark:border-white/10 rounded-lg px-3 py-2.5 text-sm text-text-primary placeholder-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all h-10 shadow-sm"
                  required
                />
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <input
                type="text"
                placeholder="Enter subject"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                className="w-full bg-card-dark dark:bg-[#0b0f1a]/80 border border-card-border dark:border-white/10 rounded-lg px-3 py-2.5 text-sm text-text-primary placeholder-slate-500 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all h-10 shadow-sm"
                required
              />
            </div>

            {success && (
              <div className="text-xs text-green-600 dark:text-green-400 bg-green-500/10 border border-green-500/20 rounded-lg p-3.5 font-medium animate-in fade-in slide-in-from-top-1 duration-200">
                Thank you! Your message was sent successfully. We will contact you soon.
              </div>
            )}

            <Button
              type="submit"
              className="px-6 py-2.5 bg-primary hover:bg-primary/95 text-white font-medium text-xs tracking-wider uppercase h-10 cursor-pointer shadow-md"
              disabled={sending}
            >
              {sending ? (
                <span className="flex items-center gap-1.5 justify-center">
                  <svg className="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Sending...
                </span>
              ) : (
                <span className="flex items-center gap-1.5 justify-center">
                  <Send className="size-3" />
                  Send Message
                </span>
              )}
            </Button>
          </form>
        </div>

        {/* Contact details Right Column */}
        <div className="md:col-span-4 space-y-8">
          <h2 className="font-serif font-bold text-2xl text-text-primary mb-8 border-b border-card-border pb-2 uppercase tracking-wider text-sm">
            Contact Details
          </h2>
          
          <div className="space-y-6">
            {/* Address */}
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shrink-0">
                <MapPin className="size-5" />
              </div>
              <div className="text-xs">
                <h4 className="font-semibold text-text-primary mb-1">Our Location</h4>
                <p className="text-text-secondary">Buttonwood, California.</p>
                <p className="text-text-secondary">Rosemead, CA 91770</p>
              </div>
            </div>

            {/* Phone */}
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shrink-0">
                <Phone className="size-5" />
              </div>
              <div className="text-xs">
                <h4 className="font-semibold text-text-primary mb-1">Direct Call</h4>
                <p className="text-text-secondary">+10 367 267 2678</p>
                <p className="text-text-secondary mt-0.5 opacity-80">Mon to Fri 9am to 6pm</p>
              </div>
            </div>

            {/* Email */}
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shrink-0">
                <Mail className="size-5" />
              </div>
              <div className="text-xs">
                <h4 className="font-semibold text-text-primary mb-1">Email Inquiry</h4>
                <p className="text-text-secondary">support@smarthotel.com</p>
                <p className="text-text-secondary mt-0.5 opacity-80">Send us your query anytime!</p>
              </div>
            </div>

            {/* Social / Website */}
            <div className="flex items-start gap-4">
              <div className="w-10 h-10 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shrink-0">
                <Globe className="size-5" />
              </div>
              <div className="text-xs">
                <h4 className="font-semibold text-text-primary mb-1">Online Channels</h4>
                <p className="text-text-secondary">www.smarthotel-resort.com</p>
                <p className="text-text-secondary mt-0.5 opacity-80">Follow @smarthotel on socials</p>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
