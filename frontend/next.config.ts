import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // A self-contained server plus only the node_modules it actually imports.
  // Without it the runtime image needs the whole dependency tree, which is most
  // of the image and none of the application.
  output: 'standalone',
};

export default nextConfig;
