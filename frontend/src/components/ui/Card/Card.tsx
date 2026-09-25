import { cn } from "@/lib/utils";
import * as React from "react";

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  hoverable?: boolean;
}

const Card = React.forwardRef<HTMLDivElement, CardProps>(
  ({ className, hoverable = false, ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn(
          "bg-card-dark border border-card-border rounded-xl p-5 backdrop-blur-md transition-all duration-300 shadow-xl shadow-glass-shadow",
          {
            "hover:-translate-y-1 hover:border-primary/30 hover:shadow-primary/5": hoverable,
          },
          className
        )}
        {...props}
      />
    );
  }
);

Card.displayName = "Card";

export { Card };
