FROM nginx:1.27-alpine

# Remove default configuration
RUN rm /etc/nginx/conf.d/default.conf

# Copy custom NGINX configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Copy SSL certificates
COPY certs/ /etc/nginx/certs/

# Expose HTTP and HTTPS ports
EXPOSE 80 443

# Start NGINX
CMD ["nginx", "-g", "daemon off;"]
