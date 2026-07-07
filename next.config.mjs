/** @type {import('next').NextConfig} */
const nextConfig = { reactStrictMode: false }; // strict mode double-mounts effects, which double-creates the WebGL context
export default nextConfig;
