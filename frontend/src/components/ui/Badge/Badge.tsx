import { cn } from "@/lib/utils";
import * as React from "react";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "info" | "success" | "warning" | "danger" | "ghost";
}

const Badge = React.forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = "info", ...props }, ref) => {
    return (
      <span
        ref={ref}
        className={cn(
          "inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold tracking-wide uppercase",
          {
            "bg-secondary/15 text-secondary border border-secondary/20": variant === "info",
            "bg-emerald-500/15 text-emerald-400 border border-emerald-500/20": variant === "success",
            "bg-amber-500/15 text-amber-400 border border-amber-500/20": variant === "warning",
            "bg-accent/15 text-accent border border-accent/20": variant === "danger",
            "bg-white/5 text-text-secondary border border-card-border": variant === "ghost",
          },
          className
        )}
        {...props}
      />
    );
  }
);

Badge.displayName = "Badge";

export { Badge };
