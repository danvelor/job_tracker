import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";
import importPlugin from "eslint-plugin-import";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    plugins: { import: importPlugin },
    settings: {
      "import/resolver": { typescript: { project: "./tsconfig.json" } },
    },
    rules: {
      // Architecture 9.4. The tree nests features inside the view rather than
      // putting them in a layer of their own, so acyclicity is reconstructed
      // from rules instead of guaranteed by layering. This is the check.
      "import/no-cycle": ["error", { maxDepth: Infinity, ignoreExternal: true }],

      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              // No slice imports another slice, and none reaches past a barrel.
              group: ["**/features/*/*"],
              message:
                "Import a slice through its index.ts. Reaching into features/<slice>/<folder> is a boundary violation (architecture 9.3).",
            },
            {
              // No slice imports the view's barrel: that closes the loop the
              // other direction and is how a barrel manufactures a false cycle.
              group: ["**/views/jobs", "**/views/jobs/index"],
              message:
                "A slice must not import the view's barrel (architecture 9.4).",
            },
          ],
        },
      ],
    },
  },
  {
    // A slice's own files reach their own folders by relative path, which the
    // pattern above would otherwise catch. The rule is about crossing INTO a
    // slice from outside, not about a slice's internals.
    files: ["src/presentation/views/*/features/*/**"],
    rules: { "no-restricted-imports": "off" },
  },
  {
    ignores: [
      "node_modules/**",
      ".next/**",
      "out/**",
      "build/**",
      "coverage/**",
      "test-results/**",
      "playwright-report/**",
      "blob-report/**",
      "next-env.d.ts",
    ],
  },
];

export default eslintConfig;
