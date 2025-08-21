/** @type {import('stylelint').Config} */
export default {
  extends: ["stylelint-config-standard"],
  // see https://stylelint.io/user-guide/rules
  rules: {
    "selector-pseudo-element-no-unknown": [true, { ignorePseudoElements: ["deep"] }],
    "alpha-value-notation": "number",
  },
};
