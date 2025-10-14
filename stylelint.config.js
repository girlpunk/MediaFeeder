/** @type {import('stylelint').Config} */
export default {
  extends: ["stylelint-config-standard"],
  // see https://stylelint.io/user-guide/rules
  rules: {
    "alpha-value-notation": "number",
    "selector-pseudo-element-no-unknown": [true, { ignorePseudoElements: ["deep"] }],
    'rule-empty-line-before': null,
    'shorthand-property-no-redundant-values': null,
  },
};
