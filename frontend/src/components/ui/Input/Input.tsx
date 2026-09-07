import * as React from "react";
import { cn } from "@/lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type = "text", label, error, ...props }, ref) => {
    return (
      <div className="w-full flex flex-col gap-1.5">
        {label ? (
          <label className="text-xs font-semibold text-text-secondary tracking-wider uppercase">
            {label}
          </label>
        ) : null}
        <input
          type={type}
          ref={ref}
          className={cn(
            "w-full px-4 py-2.5 bg-white/5 border border-card-border rounded-lg text-text-primary placeholder:text-text-secondary/50 text-sm focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/30 transition-all duration-200 backdrop-blur-md",
            {
              "border-accent focus:border-accent focus:ring-accent/30": !!error,
            },
            className
          )}
          {...props}
        />
        {error ? (
          <span className="text-xs text-accent font-medium mt-0.5">{error}</span>
        ) : null}
      </div>
    );
  }
);

Input.displayName = "Input";

export { Input };
